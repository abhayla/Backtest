﻿Imports Backtest.StrategyHelper
Imports System.Threading
Imports Algo2TradeBLL
Imports Utilities.Numbers.NumberManipulation

Public Class IntradayPositionalStrategyRule2
    Inherits StrategyRule

#Region "Entity"
    Public Class StrategyRuleEntities
        Inherits RuleEntities

        Public MaxStoplossPerTrade As Decimal
        Public TargetMultiplier As Decimal
        Public TypeOfCandle As CandleType
        Public UseOfATR As UseATR
    End Class

    Enum CandleType
        FirstCandle = 1
        SecondCandle
        SmallestCandle
    End Enum

    Enum UseATR
        None = 1
        Entry
        Stoploss
    End Enum
#End Region

    Private _atrPayload As Dictionary(Of Date, Decimal) = Nothing
    Private ReadOnly _userInputs As StrategyRuleEntities

    Public Sub New(ByVal inputPayload As Dictionary(Of Date, Payload),
                   ByVal lotSize As Integer,
                   ByVal parentStrategy As Strategy,
                   ByVal tradingDate As Date,
                   ByVal tradingSymbol As String,
                   ByVal canceller As CancellationTokenSource,
                   ByVal entities As RuleEntities)
        MyBase.New(inputPayload, lotSize, parentStrategy, tradingDate, tradingSymbol, canceller, entities)
        _userInputs = entities
    End Sub

    Public Overrides Sub CompletePreProcessing()
        MyBase.CompletePreProcessing()

        Indicator.ATR.CalculateATR(14, _signalPayload, _atrPayload)
    End Sub

    Public Overrides Async Function IsTriggerReceivedForPlaceOrderAsync(currentTick As Payload) As Task(Of Tuple(Of Boolean, List(Of PlaceOrderParameters)))
        Dim ret As Tuple(Of Boolean, List(Of PlaceOrderParameters)) = Nothing
        Await Task.Delay(0).ConfigureAwait(False)
        Dim currentMinuteCandlePayload As Payload = _signalPayload(_parentStrategy.GetCurrentXMinuteCandleTime(currentTick.PayloadDate, _signalPayload))
        Dim tradeStartTime As Date = New Date(_tradingDate.Year, _tradingDate.Month, _tradingDate.Day, _parentStrategy.TradeStartTime.Hours, _parentStrategy.TradeStartTime.Minutes, _parentStrategy.TradeStartTime.Seconds)

        Dim parameter As PlaceOrderParameters = Nothing
        If currentMinuteCandlePayload IsNot Nothing AndAlso currentMinuteCandlePayload.PreviousCandlePayload IsNot Nothing AndAlso
            Not _parentStrategy.IsTradeActive(currentTick, Trade.TypeOfTrade.MIS) AndAlso Not _parentStrategy.IsTradeOpen(currentTick, Trade.TypeOfTrade.MIS) AndAlso
            _parentStrategy.StockNumberOfTrades(currentTick.PayloadDate, currentTick.TradingSymbol) < Me._parentStrategy.NumberOfTradesPerStockPerDay AndAlso
            _parentStrategy.TotalPLAfterBrokerage(currentTick.PayloadDate) < _parentStrategy.OverAllProfitPerDay AndAlso
            _parentStrategy.TotalPLAfterBrokerage(currentTick.PayloadDate) > _parentStrategy.OverAllLossPerDay AndAlso
            _parentStrategy.StockPLAfterBrokerage(currentTick.PayloadDate, currentTick.TradingSymbol) < _parentStrategy.StockMaxProfitPerDay AndAlso
            _parentStrategy.StockPLAfterBrokerage(currentTick.PayloadDate, currentTick.TradingSymbol) > Math.Abs(_parentStrategy.StockMaxLossPerDay) * -1 AndAlso
            _parentStrategy.StockPLAfterBrokerage(currentTick.PayloadDate, currentTick.TradingSymbol) < Me.MaxProfitOfThisStock AndAlso
            _parentStrategy.StockPLAfterBrokerage(currentTick.PayloadDate, currentTick.TradingSymbol) > Math.Abs(Me.MaxLossOfThisStock) * -1 AndAlso
            currentMinuteCandlePayload.PayloadDate >= tradeStartTime AndAlso Me.EligibleToTakeTrade Then

            Dim signalCandle As Payload = Nothing
            Dim signalCandleSatisfied As Tuple(Of Boolean, Payload, Trade.TradeExecutionDirection) = GetSignalCandle(currentMinuteCandlePayload, currentTick)
            Dim lastExecutedTrade As Trade = _parentStrategy.GetLastExecutedTradeOfTheStock(currentMinuteCandlePayload, _parentStrategy.TradeType)
            If signalCandleSatisfied IsNot Nothing AndAlso signalCandleSatisfied.Item1 Then
                If lastExecutedTrade Is Nothing Then
                    signalCandle = signalCandleSatisfied.Item2
                Else
                    Dim exitCandle As Payload = _signalPayload(_parentStrategy.GetCurrentXMinuteCandleTime(lastExecutedTrade.ExitTime, _signalPayload))
                    If currentMinuteCandlePayload.PreviousCandlePayload.PayloadDate >= exitCandle.PayloadDate Then
                        signalCandle = signalCandleSatisfied.Item2
                    End If
                End If
            End If

            If signalCandle IsNot Nothing AndAlso signalCandle.PayloadDate < currentMinuteCandlePayload.PayloadDate Then
                Dim atr As Decimal = ConvertFloorCeling(_atrPayload(signalCandle.PayloadDate), _parentStrategy.TickSize, RoundOfType.Floor)
                If signalCandleSatisfied.Item3 = Trade.TradeExecutionDirection.Buy Then
                    Dim buffer As Decimal = _parentStrategy.CalculateBuffer(signalCandle.High, RoundOfType.Floor)
                    Dim entryPrice As Decimal = signalCandle.High + buffer
                    If _userInputs.UseOfATR = UseATR.Entry Then entryPrice = signalCandle.High + atr
                    Dim slPrice As Decimal = signalCandle.Low - buffer
                    If _userInputs.UseOfATR = UseATR.Stoploss Then slPrice = signalCandle.Low - atr
                    Dim slPoint As Decimal = entryPrice - slPrice
                    Dim targetPoint As Decimal = ConvertFloorCeling(slPoint * _userInputs.TargetMultiplier, _parentStrategy.TickSize, RoundOfType.Floor)
                    Dim quantity As Decimal = _parentStrategy.CalculateQuantityFromTargetSL(_tradingSymbol, entryPrice, entryPrice - slPoint, _userInputs.MaxStoplossPerTrade, _parentStrategy.StockType)

                    parameter = New PlaceOrderParameters With {
                                    .EntryPrice = entryPrice,
                                    .EntryDirection = Trade.TradeExecutionDirection.Buy,
                                    .Quantity = quantity,
                                    .Stoploss = slPrice,
                                    .Target = .EntryPrice + targetPoint,
                                    .Buffer = buffer,
                                    .SignalCandle = signalCandle,
                                    .OrderType = Trade.TypeOfOrder.SL,
                                    .Supporting1 = signalCandle.PayloadDate.ToString("HH:mm:ss"),
                                    .Supporting2 = atr
                                }
                ElseIf signalCandleSatisfied.Item3 = Trade.TradeExecutionDirection.Sell Then
                    Dim buffer As Decimal = _parentStrategy.CalculateBuffer(signalCandle.Low, RoundOfType.Floor)
                    Dim entryPrice As Decimal = signalCandle.Low - buffer
                    If _userInputs.UseOfATR = UseATR.Entry Then entryPrice = signalCandle.Low - atr
                    Dim slPrice As Decimal = signalCandle.High + buffer
                    If _userInputs.UseOfATR = UseATR.Stoploss Then slPrice = signalCandle.High + atr
                    Dim slPoint As Decimal = slPrice - entryPrice
                    Dim targetPoint As Decimal = ConvertFloorCeling(slPoint * _userInputs.TargetMultiplier, _parentStrategy.TickSize, RoundOfType.Floor)
                    Dim quantity As Decimal = _parentStrategy.CalculateQuantityFromTargetSL(_tradingSymbol, entryPrice + slPoint, entryPrice, _userInputs.MaxStoplossPerTrade, _parentStrategy.StockType)

                    parameter = New PlaceOrderParameters With {
                                    .EntryPrice = entryPrice,
                                    .EntryDirection = Trade.TradeExecutionDirection.Sell,
                                    .Quantity = quantity,
                                    .Stoploss = slPrice,
                                    .Target = .EntryPrice - targetPoint,
                                    .Buffer = buffer,
                                    .SignalCandle = signalCandle,
                                    .OrderType = Trade.TypeOfOrder.SL,
                                    .Supporting1 = signalCandle.PayloadDate.ToString("HH:mm:ss"),
                                    .Supporting2 = atr
                                }
                End If
            End If
        End If
        If parameter IsNot Nothing Then
            ret = New Tuple(Of Boolean, List(Of PlaceOrderParameters))(True, New List(Of PlaceOrderParameters) From {parameter})
        End If
        Return ret
    End Function

    Public Overrides Async Function IsTriggerReceivedForExitOrderAsync(currentTick As Payload, currentTrade As Trade) As Task(Of Tuple(Of Boolean, String))
        Dim ret As Tuple(Of Boolean, String) = Nothing
        Await Task.Delay(0).ConfigureAwait(False)
        Dim currentMinuteCandlePayload As Payload = _signalPayload(_parentStrategy.GetCurrentXMinuteCandleTime(currentTick.PayloadDate, _signalPayload))
        If currentTrade IsNot Nothing AndAlso currentTrade.TradeCurrentStatus = Trade.TradeExecutionStatus.Open Then
            Dim signalCandleSatisfied As Tuple(Of Boolean, Payload, Trade.TradeExecutionDirection) = GetSignalCandle(currentMinuteCandlePayload, currentTick)
            If signalCandleSatisfied IsNot Nothing AndAlso signalCandleSatisfied.Item1 Then
                If currentTrade.SignalCandle.PayloadDate <> signalCandleSatisfied.Item2.PayloadDate OrElse
                    currentTrade.EntryDirection <> signalCandleSatisfied.Item3 Then
                    ret = New Tuple(Of Boolean, String)(True, "Invalid Signal")
                End If
            End If
        End If
        Return ret
    End Function

    Public Overrides Async Function IsTriggerReceivedForModifyStoplossOrderAsync(currentTick As Payload, currentTrade As Trade) As Task(Of Tuple(Of Boolean, Decimal, String))
        Dim ret As Tuple(Of Boolean, Decimal, String) = Nothing
        Await Task.Delay(0).ConfigureAwait(False)
        Return ret
    End Function

    Public Overrides Async Function IsTriggerReceivedForModifyTargetOrderAsync(currentTick As Payload, currentTrade As Trade) As Task(Of Tuple(Of Boolean, Decimal, String))
        Dim ret As Tuple(Of Boolean, Decimal, String) = Nothing
        Await Task.Delay(0).ConfigureAwait(False)
        Return ret
    End Function

    Public Overrides Function IsTriggerReceivedForExitCNCEODOrderAsync(currentTick As Payload, currentTrade As Trade) As Task(Of Tuple(Of Boolean, Decimal, String))
        Throw New NotImplementedException()
    End Function

    Public Overrides Async Function UpdateRequiredCollectionsAsync(currentTick As Payload) As Task
        Await Task.Delay(0).ConfigureAwait(False)
    End Function

    Private Function GetSignalCandle(ByVal currentCandle As Payload, ByVal currentTick As Payload) As Tuple(Of Boolean, Payload, Trade.TradeExecutionDirection)
        Dim ret As Tuple(Of Boolean, Payload, Trade.TradeExecutionDirection) = Nothing
        If currentCandle.PreviousCandlePayload IsNot Nothing AndAlso Not currentCandle.PreviousCandlePayload.DeadCandle Then
            Dim signalCandle As Payload = Nothing
            If _userInputs.TypeOfCandle = CandleType.FirstCandle Then
                For Each runningPayload In _signalPayload.Values
                    If runningPayload.PayloadDate.Date = _tradingDate.Date Then
                        signalCandle = runningPayload
                        Exit For
                    End If
                Next
            ElseIf _userInputs.TypeOfCandle = CandleType.SecondCandle Then
                For Each runningPayload In _signalPayload.Values
                    If runningPayload.PayloadDate.Date = _tradingDate.Date AndAlso
                        runningPayload.PreviousCandlePayload.PayloadDate.Date = _tradingDate.Date Then
                        signalCandle = runningPayload
                        Exit For
                    End If
                Next
            ElseIf _userInputs.TypeOfCandle = CandleType.SmallestCandle Then
                If currentCandle.PreviousCandlePayload.CandleRange <= currentCandle.PreviousCandlePayload.PreviousCandlePayload.CandleRange Then
                    signalCandle = currentCandle.PreviousCandlePayload
                End If
            End If
            If signalCandle IsNot Nothing Then
                'If currentCandle.High >= signalCandle.High AndAlso
                '    currentCandle.Low <= signalCandle.Low Then
                '    If currentCandle.CandleColor = Color.Green Then
                '        ret = New Tuple(Of Boolean, Payload, Trade.TradeExecutionDirection)(True, signalCandle, Trade.TradeExecutionDirection.Sell)
                '    Else
                '        ret = New Tuple(Of Boolean, Payload, Trade.TradeExecutionDirection)(True, signalCandle, Trade.TradeExecutionDirection.Buy)
                '    End If
                'ElseIf currentCandle.High >= signalCandle.High Then
                '    ret = New Tuple(Of Boolean, Payload, Trade.TradeExecutionDirection)(True, signalCandle, Trade.TradeExecutionDirection.Buy)
                'ElseIf currentCandle.Low <= signalCandle.Low Then
                '    ret = New Tuple(Of Boolean, Payload, Trade.TradeExecutionDirection)(True, signalCandle, Trade.TradeExecutionDirection.Sell)
                'End If
                Dim middlePoint As Decimal = (signalCandle.High + signalCandle.Low) / 2
                Dim range As Decimal = signalCandle.High - middlePoint
                If currentTick.Open >= middlePoint + range * 60 / 100 Then
                    ret = New Tuple(Of Boolean, Payload, Trade.TradeExecutionDirection)(True, signalCandle, Trade.TradeExecutionDirection.Buy)
                ElseIf currentTick.Open <= middlePoint - range * 60 / 100 Then
                    ret = New Tuple(Of Boolean, Payload, Trade.TradeExecutionDirection)(True, signalCandle, Trade.TradeExecutionDirection.Sell)
                End If
            End If
        End If
        Return ret
    End Function
End Class