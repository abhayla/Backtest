﻿Imports Backtest.StrategyHelper
Imports System.Threading
Imports Algo2TradeBLL
Imports Utilities.Numbers.NumberManipulation

Public Class LowStoplossStrategyRule
    Inherits StrategyRule

#Region "Entity"
    Public Class StrategyRuleEntities
        Inherits RuleEntities

        Public StartingLevelMultiplier As Integer
        Public ChangeLevelAfterStoploss As Boolean
        Public AfterStoplossLevelMultiplier As Integer
        Public MaxStoploss As Decimal
        Public TargetMultiplier As Decimal
        Public BreakevenMovement As Boolean
        Public NumberOfTrade As Integer
        Public ModifyNumberOfTrade As Boolean
        Public MaxPLToModifyNumberOfTrade As Decimal
        Public MinimumCapital As Decimal
        Public MaxTargetPerTrade As Decimal
        Public TypeOfSignal As SignalType
    End Class

    Enum SignalType
        FractalChange = 1
        HalfVolume
        DoubleVolume
        HHHL_LLLH
        CloseOutsideFractal
        OpenCloseOutsideFractal
        DipInATR
        HalfVolumeThenIncreaseInVolume
        FirstTime
        FiveTime
        TenTime
        HKStrongCandle
        NormalStrongCandle
        SwingHighLow
        LowATRCandle
        HighVolumeLowRange
        PreviousDayHighLow
    End Enum
#End Region

    Private _potentialHighEntryPrice As Decimal = Decimal.MinValue
    Private _potentialLowEntryPrice As Decimal = Decimal.MinValue
    Private _signalCandle As Payload
    Private _entryChanged As Boolean = False
    Private _levelChanged As Boolean = False
    Private _ATRPayload As Dictionary(Of Date, Decimal) = Nothing
    Private _FractalHighPayload As Dictionary(Of Date, Decimal) = Nothing
    Private _FractalLowPayload As Dictionary(Of Date, Decimal) = Nothing
    Private _SwingHighPayload As Dictionary(Of Date, Decimal) = Nothing
    Private _SwingLowPayload As Dictionary(Of Date, Decimal) = Nothing
    Private _HKPayload As Dictionary(Of Date, Payload) = Nothing
    Private _userInputs As StrategyRuleEntities
    Private _targetRemark As String = ""
    Private _targetPoint As Decimal = Decimal.MinValue
    Private _firstEntryDirection As Trade.TradeExecutionDirection = Trade.TradeExecutionDirection.None
    Private ReadOnly _stockATR As Decimal
    Private ReadOnly _dayATR As Decimal
    Private ReadOnly _slPoint As Decimal
    Private ReadOnly _quantity As Integer

    Public Sub New(ByVal inputPayload As Dictionary(Of Date, Payload),
                   ByVal lotSize As Integer,
                   ByVal parentStrategy As Strategy,
                   ByVal tradingDate As Date,
                   ByVal tradingSymbol As String,
                   ByVal canceller As CancellationTokenSource,
                   ByVal entities As RuleEntities,
                   ByVal stockATR As Decimal,
                   ByVal dayATR As Decimal,
                   ByVal slPoint As Decimal,
                   ByVal quantity As Integer)
        MyBase.New(inputPayload, lotSize, parentStrategy, tradingDate, tradingSymbol, canceller, entities)
        _stockATR = stockATR
        _dayATR = dayATR
        _slPoint = slPoint
        _quantity = quantity
        _userInputs = New StrategyRuleEntities With {
            .StartingLevelMultiplier = CType(_entities, StrategyRuleEntities).StartingLevelMultiplier,
            .ChangeLevelAfterStoploss = CType(_entities, StrategyRuleEntities).ChangeLevelAfterStoploss,
            .AfterStoplossLevelMultiplier = CType(_entities, StrategyRuleEntities).AfterStoplossLevelMultiplier,
            .MaxStoploss = CType(_entities, StrategyRuleEntities).MaxStoploss,
            .TargetMultiplier = CType(_entities, StrategyRuleEntities).TargetMultiplier,
            .BreakevenMovement = CType(_entities, StrategyRuleEntities).BreakevenMovement,
            .NumberOfTrade = _parentStrategy.NumberOfTradesPerStockPerDay,
            .ModifyNumberOfTrade = CType(_entities, StrategyRuleEntities).ModifyNumberOfTrade,
            .MaxPLToModifyNumberOfTrade = CType(_entities, StrategyRuleEntities).MaxPLToModifyNumberOfTrade,
            .MinimumCapital = CType(_entities, StrategyRuleEntities).MinimumCapital,
            .MaxTargetPerTrade = CType(_entities, StrategyRuleEntities).MaxTargetPerTrade,
            .TypeOfSignal = CType(_entities, StrategyRuleEntities).TypeOfSignal
        }
    End Sub

    Public Overrides Sub CompletePreProcessing()
        MyBase.CompletePreProcessing()

        Indicator.ATR.CalculateATR(14, _signalPayload, _ATRPayload)
        Indicator.FractalBands.CalculateFractal(_signalPayload, _FractalHighPayload, _FractalLowPayload)
        Indicator.SwingHighLow.CalculateSwingHighLow(_signalPayload, True, _SwingHighPayload, _SwingLowPayload)
        Indicator.HeikenAshi.ConvertToHeikenAshi(_signalPayload, _HKPayload)
    End Sub

    Public Overrides Async Function IsTriggerReceivedForPlaceOrderAsync(currentTick As Payload) As Task(Of Tuple(Of Boolean, List(Of PlaceOrderParameters)))
        Dim ret As Tuple(Of Boolean, List(Of PlaceOrderParameters)) = Nothing
        Await Task.Delay(0).ConfigureAwait(False)
        Dim currentMinuteCandlePayload As Payload = _signalPayload(_parentStrategy.GetCurrentXMinuteCandleTime(currentTick.PayloadDate, _signalPayload))
        Dim tradeStartTime As Date = New Date(_tradingDate.Year, _tradingDate.Month, _tradingDate.Day, _parentStrategy.TradeStartTime.Hours, _parentStrategy.TradeStartTime.Minutes, _parentStrategy.TradeStartTime.Seconds)

        If _userInputs.ModifyNumberOfTrade Then
            If _parentStrategy.TotalPLAfterBrokerage(currentTick.PayloadDate) > _userInputs.MaxPLToModifyNumberOfTrade Then
                _userInputs.NumberOfTrade = _parentStrategy.NumberOfTradesPerStockPerDay
            Else
                If _targetRemark = "ATR Target" Then
                    _userInputs.NumberOfTrade = Math.Floor(_targetPoint / _slPoint) - 1
                End If
            End If
        End If

        Dim parameter As PlaceOrderParameters = Nothing
        If currentMinuteCandlePayload IsNot Nothing AndAlso currentMinuteCandlePayload.PreviousCandlePayload IsNot Nothing AndAlso
            Not _parentStrategy.IsTradeActive(currentTick, Trade.TypeOfTrade.MIS) AndAlso Not _parentStrategy.IsTradeOpen(currentTick, Trade.TypeOfTrade.MIS) AndAlso
            Not _parentStrategy.IsAnyTradeOfTheStockTargetReached(currentTick, Trade.TypeOfTrade.MIS) AndAlso
            _parentStrategy.StockNumberOfTrades(currentTick.PayloadDate, currentTick.TradingSymbol) < _userInputs.NumberOfTrade AndAlso
            _parentStrategy.TotalPLAfterBrokerage(currentTick.PayloadDate) < _parentStrategy.OverAllProfitPerDay AndAlso
            _parentStrategy.TotalPLAfterBrokerage(currentTick.PayloadDate) > Math.Abs(_parentStrategy.OverAllLossPerDay) * -1 AndAlso
            _parentStrategy.StockPLAfterBrokerage(currentTick.PayloadDate, currentTick.TradingSymbol) < _parentStrategy.StockMaxProfitPerDay AndAlso
            _parentStrategy.StockPLAfterBrokerage(currentTick.PayloadDate, currentTick.TradingSymbol) > Math.Abs(_parentStrategy.StockMaxLossPerDay) * -1 AndAlso
            _parentStrategy.StockPLAfterBrokerage(currentTick.PayloadDate, currentTick.TradingSymbol) < Me.MaxProfitOfThisStock AndAlso
            _parentStrategy.StockPLAfterBrokerage(currentTick.PayloadDate, currentTick.TradingSymbol) > Math.Abs(Me.MaxLossOfThisStock) * -1 AndAlso
            currentMinuteCandlePayload.PayloadDate >= tradeStartTime AndAlso Me.EligibleToTakeTrade Then

            If _userInputs.ChangeLevelAfterStoploss AndAlso Not _levelChanged Then
                Dim closeTrades As List(Of Trade) = _parentStrategy.GetSpecificTrades(currentMinuteCandlePayload, _parentStrategy.TradeType, Trade.TradeExecutionStatus.Close)
                If closeTrades IsNot Nothing AndAlso closeTrades.Count > 0 Then
                    Dim buyStoplossDone As Boolean = False
                    Dim sellStoplossDone As Boolean = False
                    For Each runningTrade In closeTrades
                        If runningTrade.ExitCondition = Trade.TradeExitCondition.StopLoss Then
                            If runningTrade.EntryDirection = Trade.TradeExecutionDirection.Buy Then
                                buyStoplossDone = True
                            ElseIf runningTrade.EntryDirection = Trade.TradeExecutionDirection.Sell Then
                                sellStoplossDone = True
                            End If
                        End If
                        If buyStoplossDone AndAlso sellStoplossDone Then
                            Exit For
                        End If
                    Next
                    If buyStoplossDone AndAlso sellStoplossDone Then
                        Dim lastExecutedTrade As Trade = _parentStrategy.GetLastExecutedTradeOfTheStock(currentMinuteCandlePayload, _parentStrategy.TradeType)
                        If lastExecutedTrade.EntryDirection = Trade.TradeExecutionDirection.Buy Then
                            _potentialLowEntryPrice = _potentialHighEntryPrice - (_userInputs.AfterStoplossLevelMultiplier + 1) * _slPoint
                        ElseIf lastExecutedTrade.EntryDirection = Trade.TradeExecutionDirection.Sell Then
                            _potentialHighEntryPrice = _potentialLowEntryPrice + (_userInputs.AfterStoplossLevelMultiplier + 1) * _slPoint
                        End If
                        _levelChanged = True
                    End If
                End If
            End If

            Dim signalCandle As Payload = Nothing

            Dim signalCandleSatisfied As Tuple(Of Boolean, Decimal, Decimal, Trade.TradeExecutionDirection) = GetSignalCandle(currentMinuteCandlePayload.PreviousCandlePayload, currentTick)
            If signalCandleSatisfied IsNot Nothing AndAlso signalCandleSatisfied.Item1 Then
                Dim lastExecutedTrade As Trade = _parentStrategy.GetLastExecutedTradeOfTheStock(currentMinuteCandlePayload, _parentStrategy.TradeType)
                If lastExecutedTrade Is Nothing Then
                    signalCandle = _signalCandle
                Else
                    If lastExecutedTrade.ExitCondition = Trade.TradeExitCondition.StopLoss AndAlso lastExecutedTrade.PLPoint > 0 Then
                        'Breakeven exit
                        'If lastExecutedTrade.EntryDirection = signalCandleSatisfied.Item4 Then
                        '    If IsSignalTriggered(signalCandleSatisfied.Item3, If(signalCandleSatisfied.Item4 = Trade.TradeExecutionDirection.Buy, Trade.TradeExecutionDirection.Sell, Trade.TradeExecutionDirection.Buy), lastExecutedTrade.ExitTime, currentTick.PayloadDate) Then
                        '        signalCandle = _signalCandle
                        '    End If
                        'Else
                        '    signalCandle = _signalCandle
                        'End If
                        'Don't take trade anymore
                    Else
                        signalCandle = _signalCandle
                    End If
                End If
            End If

            If signalCandle IsNot Nothing AndAlso signalCandle.PayloadDate < currentMinuteCandlePayload.PayloadDate Then
                Dim potentialTarget As Decimal = Decimal.MaxValue
                If _userInputs.MaxTargetPerTrade <> Decimal.MaxValue Then
                    potentialTarget = _parentStrategy.CalculatorTargetOrStoploss(_tradingSymbol, signalCandleSatisfied.Item2, _quantity, _userInputs.MaxTargetPerTrade, signalCandleSatisfied.Item4, _parentStrategy.StockType)
                End If
                If signalCandleSatisfied.Item4 = Trade.TradeExecutionDirection.Buy Then
                    Dim target As Decimal = _targetPoint
                    If potentialTarget <> Decimal.MaxValue Then target = Math.Min(_targetPoint, (potentialTarget - signalCandleSatisfied.Item2))
                    Dim buffer As Decimal = _parentStrategy.CalculateBuffer(signalCandleSatisfied.Item2, RoundOfType.Floor)
                    parameter = New PlaceOrderParameters With {
                        .EntryPrice = signalCandleSatisfied.Item2,
                        .EntryDirection = Trade.TradeExecutionDirection.Buy,
                        .Quantity = _quantity,
                        .Stoploss = signalCandleSatisfied.Item3,
                        .Target = .EntryPrice + target,
                        .Buffer = buffer,
                        .SignalCandle = signalCandle,
                        .OrderType = Trade.TypeOfOrder.SL,
                        .Supporting1 = signalCandle.PayloadDate.ToShortTimeString,
                        .Supporting2 = _targetRemark,
                        .Supporting3 = _slPoint,
                        .Supporting4 = _targetPoint,
                        .Supporting5 = _dayATR
                    }
                ElseIf signalCandleSatisfied.Item4 = Trade.TradeExecutionDirection.Sell Then
                    Dim target As Decimal = _targetPoint
                    If potentialTarget <> Decimal.MaxValue Then target = Math.Min(_targetPoint, (signalCandleSatisfied.Item2 - potentialTarget))
                    Dim buffer As Decimal = _parentStrategy.CalculateBuffer(signalCandleSatisfied.Item2, RoundOfType.Floor)
                    parameter = New PlaceOrderParameters With {
                        .EntryPrice = signalCandleSatisfied.Item2,
                        .EntryDirection = Trade.TradeExecutionDirection.Sell,
                        .Quantity = _quantity,
                        .Stoploss = signalCandleSatisfied.Item3,
                        .Target = .EntryPrice - target,
                        .Buffer = buffer,
                        .SignalCandle = signalCandle,
                        .OrderType = Trade.TypeOfOrder.SL,
                        .Supporting1 = signalCandle.PayloadDate.ToShortTimeString,
                        .Supporting2 = _targetRemark,
                        .Supporting3 = _slPoint,
                        .Supporting4 = _targetPoint,
                        .Supporting5 = _dayATR
                    }
                End If
            End If
        End If
        If parameter IsNot Nothing Then
            ret = New Tuple(Of Boolean, List(Of PlaceOrderParameters))(True, New List(Of PlaceOrderParameters) From {parameter})

            'If _parentStrategy.StockMaxProfitPercentagePerDay <> Decimal.MaxValue AndAlso Me.MaxProfitOfThisStock = Decimal.MaxValue Then
            '    Me.MaxProfitOfThisStock = _parentStrategy.CalculatePL(currentTick.TradingSymbol, parameter.EntryPrice, ConvertFloorCeling(parameter.EntryPrice + parameter.EntryPrice * _parentStrategy.StockMaxProfitPercentagePerDay / 100, _parentStrategy.TickSize, RoundOfType.Celing), parameter.Quantity, _lotSize, _parentStrategy.StockType)
            'End If
            'If _parentStrategy.StockMaxLossPercentagePerDay <> Decimal.MinValue AndAlso Me.MaxLossOfThisStock = Decimal.MinValue Then
            '    Me.MaxLossOfThisStock = _parentStrategy.CalculatePL(currentTick.TradingSymbol, parameter.EntryPrice, ConvertFloorCeling(parameter.EntryPrice - parameter.EntryPrice * _parentStrategy.StockMaxLossPercentagePerDay / 100, _parentStrategy.TickSize, RoundOfType.Celing), parameter.Quantity, _lotSize, _parentStrategy.StockType)
            'End If
        End If
        Return ret
    End Function

    Public Overrides Async Function IsTriggerReceivedForExitOrderAsync(currentTick As Payload, currentTrade As Trade) As Task(Of Tuple(Of Boolean, String))
        Dim ret As Tuple(Of Boolean, String) = Nothing
        Await Task.Delay(0).ConfigureAwait(False)
        Dim currentMinuteCandlePayload As Payload = _signalPayload(_parentStrategy.GetCurrentXMinuteCandleTime(currentTick.PayloadDate, _signalPayload))

        If currentTrade IsNot Nothing AndAlso currentTrade.TradeCurrentStatus = Trade.TradeExecutionStatus.Open Then
            Dim signalCandle As Payload = currentTrade.SignalCandle
            If signalCandle IsNot Nothing Then
                Dim signalCandleSatisfied As Tuple(Of Boolean, Decimal, Decimal, Trade.TradeExecutionDirection) = GetSignalCandle(currentMinuteCandlePayload.PreviousCandlePayload, currentTick)
                If signalCandleSatisfied IsNot Nothing AndAlso signalCandleSatisfied.Item1 Then
                    Dim entryPrice As Decimal = Decimal.MinValue
                    If signalCandleSatisfied.Item4 = Trade.TradeExecutionDirection.Buy Then
                        entryPrice = signalCandleSatisfied.Item2
                    ElseIf signalCandleSatisfied.Item4 = Trade.TradeExecutionDirection.Sell Then
                        entryPrice = signalCandleSatisfied.Item2
                    End If
                    If (currentTrade.EntryDirection <> signalCandleSatisfied.Item4) OrElse
                        (currentTrade.EntryPrice <> entryPrice) Then
                        ret = New Tuple(Of Boolean, String)(True, "Invalid Signal")
                    End If
                End If
            End If
        End If
        Return ret
    End Function

    Public Overrides Async Function IsTriggerReceivedForModifyStoplossOrderAsync(currentTick As Payload, currentTrade As Trade) As Task(Of Tuple(Of Boolean, Decimal, String))
        Dim ret As Tuple(Of Boolean, Decimal, String) = Nothing
        Await Task.Delay(0).ConfigureAwait(False)
        If _userInputs.BreakevenMovement AndAlso currentTrade IsNot Nothing AndAlso currentTrade.TradeCurrentStatus = Trade.TradeExecutionStatus.Inprogress Then
            Dim triggerPrice As Decimal = Decimal.MinValue
            If currentTrade.EntryDirection = Trade.TradeExecutionDirection.Buy Then
                Dim slPoint As Decimal = currentTrade.EntryPrice - currentTrade.PotentialStopLoss
                Dim slPL As Decimal = _parentStrategy.CalculatePL(_tradingSymbol, currentTrade.EntryPrice, currentTrade.EntryPrice - slPoint, currentTrade.Quantity, LotSize, _parentStrategy.StockType)
                Dim targetPL As Decimal = Math.Abs(slPL) * (_userInputs.TargetMultiplier + 1)
                Dim excpectedTarget As Decimal = _parentStrategy.CalculatorTargetOrStoploss(_tradingSymbol, currentTrade.EntryPrice, currentTrade.Quantity, targetPL, Trade.TradeExecutionDirection.Buy, _parentStrategy.StockType)
                If currentTick.Open >= excpectedTarget Then
                    triggerPrice = _parentStrategy.CalculatorTargetOrStoploss(_tradingSymbol, currentTrade.EntryPrice, currentTrade.Quantity, Math.Abs(slPL) * _userInputs.TargetMultiplier, Trade.TradeExecutionDirection.Buy, _parentStrategy.StockType)
                End If
            ElseIf currentTrade.EntryDirection = Trade.TradeExecutionDirection.Sell Then
                Dim slPoint As Decimal = currentTrade.PotentialStopLoss - currentTrade.EntryPrice
                Dim slPL As Decimal = _parentStrategy.CalculatePL(_tradingSymbol, currentTrade.EntryPrice + slPoint, currentTrade.EntryPrice, currentTrade.Quantity, LotSize, _parentStrategy.StockType)
                Dim targetPL As Decimal = Math.Abs(slPL) * (_userInputs.TargetMultiplier + 1)
                Dim excpectedTarget As Decimal = _parentStrategy.CalculatorTargetOrStoploss(_tradingSymbol, currentTrade.EntryPrice, currentTrade.Quantity, targetPL, Trade.TradeExecutionDirection.Sell, _parentStrategy.StockType)
                If currentTick.Open <= excpectedTarget Then
                    triggerPrice = _parentStrategy.CalculatorTargetOrStoploss(_tradingSymbol, currentTrade.EntryPrice, currentTrade.Quantity, Math.Abs(slPL) * _userInputs.TargetMultiplier, Trade.TradeExecutionDirection.Sell, _parentStrategy.StockType)
                End If
            End If
            If triggerPrice <> Decimal.MinValue AndAlso triggerPrice <> currentTrade.PotentialStopLoss Then
                ret = New Tuple(Of Boolean, Decimal, String)(True, triggerPrice, String.Format("Move to breakeven: {0}. Time:{1}", triggerPrice, currentTick.PayloadDate))
            End If
        End If
        Return ret
    End Function

    Public Overrides Async Function IsTriggerReceivedForModifyTargetOrderAsync(currentTick As Payload, currentTrade As Trade) As Task(Of Tuple(Of Boolean, Decimal, String))
        Dim ret As Tuple(Of Boolean, Decimal, String) = Nothing
        Await Task.Delay(0).ConfigureAwait(False)
        Return ret
    End Function

    Public Overrides Async Function UpdateRequiredCollectionsAsync(currentTick As Payload) As Task
        Await Task.Delay(0).ConfigureAwait(False)
        If Not _entryChanged AndAlso _slPoint <> Decimal.MinValue AndAlso
            _potentialHighEntryPrice <> Decimal.MinValue AndAlso _potentialLowEntryPrice <> Decimal.MinValue Then
            Dim highBuffer As Decimal = _parentStrategy.CalculateBuffer(_potentialHighEntryPrice, RoundOfType.Floor)
            Dim lowBuffer As Decimal = _parentStrategy.CalculateBuffer(_potentialLowEntryPrice, RoundOfType.Floor)
            If _firstEntryDirection = Trade.TradeExecutionDirection.None Then
                If currentTick.High >= _potentialHighEntryPrice + highBuffer Then
                    _potentialHighEntryPrice = _potentialHighEntryPrice + highBuffer
                    _potentialLowEntryPrice = _potentialHighEntryPrice - (_userInputs.StartingLevelMultiplier + 1) * _slPoint
                    _entryChanged = True
                ElseIf currentTick.Low <= _potentialLowEntryPrice - lowBuffer Then
                    _potentialLowEntryPrice = _potentialLowEntryPrice - lowBuffer
                    _potentialHighEntryPrice = _potentialLowEntryPrice + (_userInputs.StartingLevelMultiplier + 1) * _slPoint
                    _entryChanged = True
                End If
            Else
                If _firstEntryDirection = Trade.TradeExecutionDirection.Buy Then
                    If currentTick.High >= _potentialHighEntryPrice + highBuffer Then
                        _potentialHighEntryPrice = _potentialHighEntryPrice + highBuffer
                        _potentialLowEntryPrice = _potentialHighEntryPrice - (_userInputs.StartingLevelMultiplier + 1) * _slPoint
                        _entryChanged = True
                    End If
                ElseIf _firstEntryDirection = Trade.TradeExecutionDirection.Sell Then
                    If currentTick.Low <= _potentialLowEntryPrice - lowBuffer Then
                        _potentialLowEntryPrice = _potentialLowEntryPrice - lowBuffer
                        _potentialHighEntryPrice = _potentialLowEntryPrice + (_userInputs.StartingLevelMultiplier + 1) * _slPoint
                        _entryChanged = True
                    End If
                End If
            End If
        End If
    End Function

    Private Function GetSignalCandle(ByVal candle As Payload, ByVal currentTick As Payload) As Tuple(Of Boolean, Decimal, Decimal, Trade.TradeExecutionDirection)
        Dim ret As Tuple(Of Boolean, Decimal, Decimal, Trade.TradeExecutionDirection) = Nothing
        If candle IsNot Nothing AndAlso candle.PreviousCandlePayload IsNot Nothing AndAlso
            Not candle.DeadCandle AndAlso Not candle.PreviousCandlePayload.DeadCandle Then
            Dim lastExecutedTrade As Trade = _parentStrategy.GetLastExecutedTradeOfTheStock(candle, _parentStrategy.TradeType)
            Dim signalFound As Boolean = False
            Dim iSHeikenAshi As Boolean = False
            Dim isPreviousCandle As Boolean = False

            Select Case _userInputs.TypeOfSignal
                Case SignalType.FractalChange
                    If _potentialHighEntryPrice = Decimal.MinValue AndAlso _potentialLowEntryPrice = Decimal.MinValue Then
                        If IsFractalChangeSignalCandle(candle) Then
                            signalFound = True
                        End If
                    End If
                Case SignalType.HalfVolume
                    If lastExecutedTrade Is Nothing AndAlso Not _entryChanged Then
                        If IsHalfVolumeSignalCandle(candle) Then
                            signalFound = True
                        End If
                    End If
                Case SignalType.DoubleVolume
                    If lastExecutedTrade Is Nothing AndAlso Not _entryChanged Then
                        If IsDoubleSignalCandle(candle) Then
                            signalFound = True
                        End If
                    End If
                Case SignalType.HHHL_LLLH
                    If lastExecutedTrade Is Nothing AndAlso Not _entryChanged Then
                        If IsHHHLLHLLSignalCandle(candle) Then
                            signalFound = True
                        End If
                    End If
                Case SignalType.CloseOutsideFractal
                    If lastExecutedTrade Is Nothing AndAlso Not _entryChanged Then
                        If IsCloseOutsideFractalSignalCandle(candle) Then
                            signalFound = True
                        End If
                    End If
                Case SignalType.OpenCloseOutsideFractal
                    If lastExecutedTrade Is Nothing AndAlso Not _entryChanged Then
                        If IsOpenCloseOutsideFractalSignalCandle(candle) Then
                            signalFound = True
                        End If
                    End If
                Case SignalType.DipInATR
                    If lastExecutedTrade Is Nothing AndAlso Not _entryChanged Then
                        If IsDipInATRSignalCandle(candle) Then
                            signalFound = True
                        End If
                    End If
                Case SignalType.HalfVolumeThenIncreaseInVolume
                    If _potentialHighEntryPrice = Decimal.MinValue AndAlso _potentialLowEntryPrice = Decimal.MinValue Then
                        If IsHalfVolumeThenIncreaseInVolumeSignalCandle(candle) Then
                            signalFound = True
                        End If
                    End If
                Case SignalType.FirstTime
                    If lastExecutedTrade Is Nothing AndAlso Not _entryChanged Then
                        If IsnMinutesSignalCandle(candle, "09:16:00") Then
                            signalFound = True
                        End If
                    End If
                Case SignalType.FiveTime
                    If lastExecutedTrade Is Nothing AndAlso Not _entryChanged Then
                        If IsnMinutesSignalCandle(candle, "09:20:00") Then
                            signalFound = True
                        End If
                    End If
                Case SignalType.TenTime
                    If lastExecutedTrade Is Nothing AndAlso Not _entryChanged Then
                        If IsnMinutesSignalCandle(candle, "09:25:00") Then
                            signalFound = True
                        End If
                    End If
                Case SignalType.HKStrongCandle
                    If lastExecutedTrade Is Nothing AndAlso Not _entryChanged Then
                        If IsHKStrongCandleSignalCandle(candle) Then
                            signalFound = True
                            iSHeikenAshi = True
                        End If
                    End If
                Case SignalType.NormalStrongCandle
                    If lastExecutedTrade Is Nothing AndAlso Not _entryChanged Then
                        If IsNormalStrongCandleSignalCandle(candle) Then
                            signalFound = True
                            If candle.CandleColor = Color.Red Then
                                _firstEntryDirection = Trade.TradeExecutionDirection.Buy
                            ElseIf candle.CandleColor = Color.Green Then
                                _firstEntryDirection = Trade.TradeExecutionDirection.Sell
                            End If
                        End If
                    End If
                Case SignalType.LowATRCandle
                    If lastExecutedTrade Is Nothing AndAlso Not _entryChanged Then
                        If IsLowATRSignalCandle(candle) Then
                            signalFound = True
                        End If
                    End If
                Case SignalType.SwingHighLow
                    If lastExecutedTrade Is Nothing AndAlso Not _entryChanged Then
                        If IsSwingHighLowSignalCandle(candle) Then
                            signalFound = True
                            If _SwingHighPayload(candle.PayloadDate) = candle.PreviousCandlePayload.High Then
                                _firstEntryDirection = Trade.TradeExecutionDirection.Buy
                            ElseIf _SwingLowPayload(candle.PayloadDate) = candle.PreviousCandlePayload.Low Then
                                _firstEntryDirection = Trade.TradeExecutionDirection.Sell
                            End If
                            isPreviousCandle = True
                        End If
                    End If
                Case SignalType.HighVolumeLowRange
                    If _potentialHighEntryPrice = Decimal.MinValue AndAlso _potentialLowEntryPrice = Decimal.MinValue Then
                        If IsHighVolumeLowRangeSignalCandle(candle) Then
                            signalFound = True
                        End If
                    End If
                Case SignalType.PreviousDayHighLow
                    If lastExecutedTrade Is Nothing AndAlso Not _entryChanged Then
                        signalFound = True
                    End If
            End Select

            If signalFound Then
                If iSHeikenAshi Then
                    Dim hkCandle As Payload = _HKPayload(candle.PayloadDate)
                    _potentialHighEntryPrice = ConvertFloorCeling(hkCandle.High, _parentStrategy.TickSize, RoundOfType.Floor)
                    _potentialLowEntryPrice = ConvertFloorCeling(hkCandle.Low, _parentStrategy.TickSize, RoundOfType.Celing)
                ElseIf isPreviousCandle Then
                    _potentialHighEntryPrice = candle.PreviousCandlePayload.High
                    _potentialLowEntryPrice = candle.PreviousCandlePayload.Low
                Else
                    _potentialHighEntryPrice = candle.High
                    _potentialLowEntryPrice = candle.Low
                End If
                _signalCandle = candle
                Dim atr As Decimal = _ATRPayload(_signalCandle.PayloadDate)
                Dim pl As Decimal = _parentStrategy.CalculatePL(_tradingSymbol, _potentialHighEntryPrice, _potentialHighEntryPrice - (_slPoint + _parentStrategy.TickSize), _quantity, LotSize, _parentStrategy.StockType)
                Dim target As Decimal = _parentStrategy.CalculatorTargetOrStoploss(_tradingSymbol, _potentialHighEntryPrice, _quantity, Math.Abs(pl) * _userInputs.TargetMultiplier, Trade.TradeExecutionDirection.Buy, _parentStrategy.StockType)

                If ConvertFloorCeling(atr * _userInputs.TargetMultiplier, _parentStrategy.TickSize, RoundOfType.Celing) >= target - _potentialHighEntryPrice Then
                    _targetPoint = ConvertFloorCeling(atr * _userInputs.TargetMultiplier, _parentStrategy.TickSize, RoundOfType.Celing)
                    _targetRemark = "ATR Target"
                    If _targetPoint > _dayATR / 2 Then
                        '_potentialHighEntryPrice = Decimal.MinValue
                        '_potentialLowEntryPrice = Decimal.MinValue
                        '_signalCandle = Nothing
                        _targetPoint = ConvertFloorCeling(_dayATR / 2, _parentStrategy.TickSize, RoundOfType.Celing)
                        _targetRemark = "Day ATR Target"
                    End If
                    If _userInputs.ModifyNumberOfTrade Then
                        _userInputs.NumberOfTrade = Math.Floor(_targetPoint / _slPoint) - 1
                    End If
                Else
                    _targetPoint = target - _potentialHighEntryPrice
                    _targetRemark = "SL Target"
                End If
            End If

            If _potentialHighEntryPrice <> Decimal.MinValue AndAlso _potentialLowEntryPrice <> Decimal.MinValue Then
                If _entryChanged Then
                    Dim middlePoint As Decimal = (_potentialHighEntryPrice + _potentialLowEntryPrice) / 2
                    Dim range As Decimal = _potentialHighEntryPrice - middlePoint
                    If currentTick.Open >= middlePoint + range * 60 / 100 Then
                        ret = New Tuple(Of Boolean, Decimal, Decimal, Trade.TradeExecutionDirection)(True, _potentialHighEntryPrice, _potentialHighEntryPrice - _slPoint, Trade.TradeExecutionDirection.Buy)
                    ElseIf currentTick.Open <= middlePoint - range * 60 / 100 Then
                        ret = New Tuple(Of Boolean, Decimal, Decimal, Trade.TradeExecutionDirection)(True, _potentialLowEntryPrice, _potentialLowEntryPrice + _slPoint, Trade.TradeExecutionDirection.Sell)
                    End If
                Else
                    Dim tradeDirection As Trade.TradeExecutionDirection = Trade.TradeExecutionDirection.None
                    Dim buffer As Decimal = Decimal.MinValue
                    If _firstEntryDirection = Trade.TradeExecutionDirection.None Then
                        Dim middlePoint As Decimal = (_potentialHighEntryPrice + _potentialLowEntryPrice) / 2
                        Dim range As Decimal = _potentialHighEntryPrice - middlePoint
                        If currentTick.Open >= middlePoint + range * 30 / 100 Then
                            tradeDirection = Trade.TradeExecutionDirection.Buy
                            buffer = _parentStrategy.CalculateBuffer(_potentialHighEntryPrice, RoundOfType.Floor)
                        ElseIf currentTick.Open <= middlePoint - range * 30 / 100 Then
                            tradeDirection = Trade.TradeExecutionDirection.Sell
                            buffer = _parentStrategy.CalculateBuffer(_potentialLowEntryPrice, RoundOfType.Floor)
                        End If
                    Else
                        tradeDirection = _firstEntryDirection
                        If tradeDirection = Trade.TradeExecutionDirection.Buy Then
                            buffer = _parentStrategy.CalculateBuffer(_potentialHighEntryPrice, RoundOfType.Floor)
                        ElseIf tradeDirection = Trade.TradeExecutionDirection.Sell Then
                            buffer = _parentStrategy.CalculateBuffer(_potentialLowEntryPrice, RoundOfType.Floor)
                        End If
                    End If
                    If tradeDirection = Trade.TradeExecutionDirection.Buy Then
                        ret = New Tuple(Of Boolean, Decimal, Decimal, Trade.TradeExecutionDirection)(True, _potentialHighEntryPrice + buffer, _potentialHighEntryPrice + buffer - _slPoint, Trade.TradeExecutionDirection.Buy)
                    ElseIf tradeDirection = Trade.TradeExecutionDirection.Sell Then
                        ret = New Tuple(Of Boolean, Decimal, Decimal, Trade.TradeExecutionDirection)(True, _potentialLowEntryPrice - buffer, _potentialLowEntryPrice - buffer + _slPoint, Trade.TradeExecutionDirection.Sell)
                    End If
                End If
            End If
        End If
        Return ret
    End Function

#Region "Fractal Change"
    Private Function IsFractalChangeSignalCandle(ByVal candle As Payload) As Boolean
        Dim ret As Boolean = False
        If IsFractalChanged(candle.PayloadDate) = 1 AndAlso candle.Low < candle.PreviousCandlePayload.Low Then
            ret = True
        ElseIf IsFractalChanged(candle.PayloadDate) = -1 AndAlso candle.High > candle.PreviousCandlePayload.High Then
            ret = True
        End If
        Return ret
    End Function

    Private Function IsFractalChanged(ByVal currentTime As Date) As Integer
        Dim ret As Integer = False
        If _signalPayload IsNot Nothing AndAlso _signalPayload.Count > 0 Then
            For Each runningPayload In _signalPayload
                If runningPayload.Key.Date = _tradingDate.Date AndAlso runningPayload.Key <= currentTime Then
                    If _FractalHighPayload(runningPayload.Value.PayloadDate) <> _FractalHighPayload(runningPayload.Value.PreviousCandlePayload.PayloadDate) Then
                        ret = 1
                        Exit For
                    End If
                    If _FractalLowPayload(runningPayload.Value.PayloadDate) <> _FractalLowPayload(runningPayload.Value.PreviousCandlePayload.PayloadDate) Then
                        ret = -1
                        Exit For
                    End If
                End If
            Next
        End If
        Return ret
    End Function
#End Region

#Region "n Minutes"
    Private Function IsnMinutesSignalCandle(ByVal candle As Payload, ByVal timeToMatch As String) As Boolean
        Dim ret As Boolean = False
        If candle.PayloadDate.Date = _tradingDate.Date AndAlso candle.PayloadDate.ToString("HH:mm:ss") = timeToMatch Then
            ret = True
        End If
        Return ret
    End Function
#End Region

#Region "Half Volume"
    Private Function IsHalfVolumeSignalCandle(ByVal candle As Payload) As Boolean
        Dim ret As Boolean = False
        If candle.Volume <= candle.PreviousCandlePayload.Volume * 0.5 Then
            ret = True
        End If
        Return ret
    End Function
#End Region

#Region "Double Volume"
    Private Function IsDoubleSignalCandle(ByVal candle As Payload) As Boolean
        Dim ret As Boolean = False
        If candle.Volume >= candle.PreviousCandlePayload.Volume * 1.9 Then
            ret = True
        End If
        Return ret
    End Function
#End Region

#Region "HH-HL/LH-LL"
    Private Function IsHHHLLHLLSignalCandle(ByVal candle As Payload) As Boolean
        Dim ret As Boolean = False
        If candle.High > candle.PreviousCandlePayload.High AndAlso candle.Low > candle.PreviousCandlePayload.Low Then
            ret = True
        ElseIf candle.High < candle.PreviousCandlePayload.High AndAlso candle.Low < candle.PreviousCandlePayload.Low Then
            ret = True
        End If
        Return ret
    End Function
#End Region

#Region "Close outside fractal"
    Private Function IsCloseOutsideFractalSignalCandle(ByVal candle As Payload) As Boolean
        Dim ret As Boolean = False
        If candle.Close > _FractalHighPayload(candle.PayloadDate) OrElse candle.Close < _FractalLowPayload(candle.PayloadDate) Then
            ret = True
        End If
        Return ret
    End Function
#End Region

#Region "Candle whose open close is outslide fractal"
    Private Function IsOpenCloseOutsideFractalSignalCandle(ByVal candle As Payload) As Boolean
        Dim ret As Boolean = False
        If candle.Open > _FractalHighPayload(candle.PayloadDate) AndAlso candle.Close > _FractalHighPayload(candle.PayloadDate) Then
            ret = True
        ElseIf candle.Open < _FractalLowPayload(candle.PayloadDate) AndAlso candle.Close < _FractalLowPayload(candle.PayloadDate) Then
            ret = True
        End If
        Return ret
    End Function
#End Region

#Region "1% dip in ATR"
    Private Function IsDipInATRSignalCandle(ByVal candle As Payload) As Boolean
        Dim ret As Boolean = False
        If _ATRPayload(candle.PayloadDate) <= _ATRPayload(candle.PreviousCandlePayload.PayloadDate) * 0.99 Then
            ret = True
        End If
        Return ret
    End Function
#End Region

#Region "Half volume and then first increase in volume"
    Private Function IsHalfVolumeThenIncreaseInVolumeSignalCandle(ByVal candle As Payload) As Boolean
        Dim ret As Boolean = False
        If candle.Volume > candle.PreviousCandlePayload.Volume Then
            If IsHalfVolume(candle.PayloadDate) Then
                ret = True
            End If
        End If
        Return ret
    End Function

    Private Function IsHalfVolume(ByVal currentTime As Date) As Boolean
        Dim ret As Boolean = False
        For Each runningPayload In _signalPayload
            If runningPayload.Key.Date = _tradingDate.Date AndAlso runningPayload.Key < currentTime Then
                If runningPayload.Value.Volume <= runningPayload.Value.PreviousCandlePayload.Volume / 2 Then
                    ret = True
                    Exit For
                End If
            End If
        Next
        Return ret
    End Function
#End Region

#Region "HK 2 Strong Candle"
    Private Function IsHKStrongCandleSignalCandle(ByVal candle As Payload) As Boolean
        Dim ret As Boolean = False
        Dim hkCandle As Payload = _HKPayload(candle.PayloadDate)
        If IsHK2StrongCandleFound(candle.PayloadDate) Then
            If hkCandle.CandleStrengthHeikenAshi = Payload.StrongCandle.Bearish OrElse hkCandle.CandleStrengthHeikenAshi = Payload.StrongCandle.Bullish Then
                ret = True
            End If
        End If
        Return ret
    End Function

    Private Function IsHK2StrongCandleFound(ByVal currentTime As Date) As Boolean
        Dim ret As Boolean = False
        For Each runningPayload In _HKPayload
            If runningPayload.Key.Date = _tradingDate.Date AndAlso
                runningPayload.Value.PreviousCandlePayload.PayloadDate.Date = _tradingDate.Date AndAlso
                runningPayload.Key <= currentTime Then
                Dim buffer As Decimal = _parentStrategy.CalculateBuffer(runningPayload.Value.High, RoundOfType.Floor)
                If runningPayload.Value.CandleStrengthHeikenAshi(buffer - _parentStrategy.TickSize) = Payload.StrongCandle.Bullish AndAlso
                    runningPayload.Value.PreviousCandlePayload.CandleStrengthHeikenAshi(buffer - _parentStrategy.TickSize) = Payload.StrongCandle.Bullish Then
                    If runningPayload.Value.High >= runningPayload.Value.PreviousCandlePayload.High Then
                        ret = True
                        Exit For
                    End If
                ElseIf runningPayload.Value.CandleStrengthHeikenAshi(buffer - _parentStrategy.TickSize) = Payload.StrongCandle.Bearish AndAlso
                    runningPayload.Value.PreviousCandlePayload.CandleStrengthHeikenAshi(buffer - _parentStrategy.TickSize) = Payload.StrongCandle.Bearish Then
                    If runningPayload.Value.Low <= runningPayload.Value.PreviousCandlePayload.Low Then
                        ret = True
                        Exit For
                    End If
                End If
            End If
        Next
        Return ret
    End Function
#End Region

#Region "Normal Strong Candle"
    Private Function IsNormalStrongCandleSignalCandle(ByVal candle As Payload) As Boolean
        Dim ret As Boolean = False
        Dim currentMinuteCandlePayload As Payload = _signalPayload(_parentStrategy.GetCurrentXMinuteCandleTime(candle.PayloadDate.AddMinutes(_parentStrategy.SignalTimeFrame), _signalPayload))

        If candle.CandleColor = Color.Green AndAlso
            candle.CandleWicks.Top <= candle.CandleRange * 25 / 100 AndAlso
            candle.CandleWicks.Bottom <= candle.CandleRange * 50 / 100 Then
            Dim buffer As Decimal = _parentStrategy.CalculateBuffer(candle.Low, RoundOfType.Floor)
            If currentMinuteCandlePayload.Low <= candle.Low - buffer Then
                ret = True
            End If
        ElseIf candle.CandleColor = Color.Red AndAlso
            candle.CandleWicks.Top <= candle.CandleRange * 50 / 100 AndAlso
            candle.CandleWicks.Bottom <= candle.CandleRange * 25 / 100 Then
            Dim buffer As Decimal = _parentStrategy.CalculateBuffer(candle.Low, RoundOfType.Floor)
            If currentMinuteCandlePayload.High >= candle.High + buffer Then
                ret = True
            End If
        End If
        Return ret
    End Function
#End Region

#Region "Low ATR Candle"
    Private Function IsLowATRSignalCandle(ByVal candle As Payload) As Boolean
        Dim ret As Boolean = False
        If candle.CandleRange <= _ATRPayload(candle.PayloadDate) / 2 Then
            ret = True
        End If
        Return ret
    End Function
#End Region

#Region "Swing High Low"
    Private Function IsSwingHighLowSignalCandle(ByVal candle As Payload) As Boolean
        Dim ret As Boolean = False
        Dim currentMinuteCandlePayload As Payload = _signalPayload(_parentStrategy.GetCurrentXMinuteCandleTime(candle.PayloadDate.AddMinutes(_parentStrategy.SignalTimeFrame), _signalPayload))
        If _SwingHighPayload(candle.PayloadDate) = candle.PreviousCandlePayload.High Then
            ret = True
        ElseIf _SwingLowPayload(candle.PayloadDate) = candle.PreviousCandlePayload.Low Then
            ret = True
        End If
        Return ret
    End Function
#End Region

#Region "High Volume Low Range Candle"
    Private Function IsHighVolumeLowRangeSignalCandle(ByVal candle As Payload) As Boolean
        Dim ret As Boolean = False
        If candle.Volume > candle.PreviousCandlePayload.Volume AndAlso
            candle.CandleRange < candle.PreviousCandlePayload.CandleRange Then
            ret = True
        End If
        Return ret
    End Function

    Public Overrides Function IsTriggerReceivedForExitCNCEODOrderAsync(currentTick As Payload, currentTrade As Trade) As Task(Of Tuple(Of Boolean, Decimal, String))
        Throw New NotImplementedException()
    End Function
#End Region

End Class
