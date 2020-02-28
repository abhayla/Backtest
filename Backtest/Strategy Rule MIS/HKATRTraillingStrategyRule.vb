﻿Imports Backtest.StrategyHelper
Imports System.Threading
Imports Algo2TradeBLL
Imports Utilities.Numbers.NumberManipulation

Public Class HKATRTraillingStrategyRule
    Inherits StrategyRule

#Region "Entity"
    Public Class StrategyRuleEntities
        Inherits RuleEntities

        Public ATRTargetMultiplier As Decimal
        Public SLTargetMultiplier As Decimal
    End Class
#End Region

    Private ReadOnly _userInputs As StrategyRuleEntities

    Private _hkPayloads As Dictionary(Of Date, Payload)
    Private _atrPayloads As Dictionary(Of Date, Decimal)
    Private _atrTrailingPayloads As Dictionary(Of Date, Decimal)
    Private _atrTrailingColorPayloads As Dictionary(Of Date, Color)

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

        Indicator.HeikenAshi.ConvertToHeikenAshi(_signalPayload, _hkPayloads)
        Indicator.ATR.CalculateATR(14, _hkPayloads, _atrPayloads)
        Indicator.ATRTrailingStop.CalculateATRTrailingStop(7, 2, _hkPayloads, _atrTrailingPayloads, _atrTrailingColorPayloads)
    End Sub

    Public Overrides Async Function IsTriggerReceivedForPlaceOrderAsync(ByVal currentTick As Payload) As Task(Of Tuple(Of Boolean, List(Of PlaceOrderParameters)))
        Dim ret As Tuple(Of Boolean, List(Of PlaceOrderParameters)) = Nothing
        Await Task.Delay(0).ConfigureAwait(False)
        Dim currentMinuteCandlePayload As Payload = _signalPayload(_parentStrategy.GetCurrentXMinuteCandleTime(currentTick.PayloadDate, _signalPayload))
        Dim tradeStartTime As Date = New Date(_tradingDate.Year, _tradingDate.Month, _tradingDate.Day, _parentStrategy.TradeStartTime.Hours, _parentStrategy.TradeStartTime.Minutes, _parentStrategy.TradeStartTime.Seconds)
        Dim stkTrdNmbr As Integer = _parentStrategy.StockNumberOfTrades(currentTick.PayloadDate, currentTick.TradingSymbol)
        Dim ttlPL As Decimal = _parentStrategy.TotalPLAfterBrokerage(currentTick.PayloadDate)
        Dim stkPL As Decimal = _parentStrategy.StockPLAfterBrokerage(currentTick.PayloadDate, currentTick.TradingSymbol)
        Dim parameter As PlaceOrderParameters = Nothing
        If currentMinuteCandlePayload IsNot Nothing AndAlso currentMinuteCandlePayload.PreviousCandlePayload IsNot Nothing AndAlso
            Not _parentStrategy.IsTradeActive(currentTick, _parentStrategy.TradeType) AndAlso Not _parentStrategy.IsTradeOpen(currentTick, _parentStrategy.TradeType) AndAlso
            stkTrdNmbr < Me._parentStrategy.NumberOfTradesPerStockPerDay AndAlso ttlPL < _parentStrategy.OverAllProfitPerDay AndAlso ttlPL > _parentStrategy.OverAllLossPerDay AndAlso
            stkPL < _parentStrategy.StockMaxProfitPerDay AndAlso stkPL > Math.Abs(_parentStrategy.StockMaxLossPerDay) * -1 AndAlso
            stkPL < Me.MaxProfitOfThisStock AndAlso stkPL > Math.Abs(Me.MaxLossOfThisStock) * -1 AndAlso
            currentMinuteCandlePayload.PayloadDate >= tradeStartTime AndAlso Me.EligibleToTakeTrade Then

            Dim signalCandle As Payload = Nothing
            Dim signal As Tuple(Of Boolean, Decimal, Trade.TradeExecutionDirection, Payload) = GetSignalCandle(currentMinuteCandlePayload.PreviousCandlePayload, currentTick)
            If signal IsNot Nothing AndAlso signal.Item1 Then
                signalCandle = signal.Item4
            End If

            If signalCandle IsNot Nothing AndAlso signalCandle.PayloadDate < currentMinuteCandlePayload.PayloadDate Then
                If signal.Item3 = Trade.TradeExecutionDirection.Buy Then
                    Dim buffer As Decimal = _parentStrategy.CalculateBuffer(signalCandle.High, RoundOfType.Floor)
                    Dim entry As Decimal = signalCandle.High + buffer
                    Dim stoploss As Decimal = signalCandle.Low - buffer
                    Dim slTargetPoint As Decimal = ConvertFloorCeling(Math.Abs(entry - stoploss) * _userInputs.SLTargetMultiplier, _parentStrategy.TickSize, RoundOfType.Floor)
                    Dim atrTargetPoint As Decimal = ConvertFloorCeling(_atrPayloads(signalCandle.PayloadDate) * _userInputs.ATRTargetMultiplier, _parentStrategy.TickSize, RoundOfType.Floor)
                    Dim target As Decimal = Decimal.MinValue
                    Dim targetRemark As String = Nothing
                    If atrTargetPoint <= slTargetPoint Then
                        target = entry + atrTargetPoint
                        targetRemark = "ATR Target"
                    Else
                        target = entry + slTargetPoint
                        targetRemark = "SL Target"
                    End If
                    parameter = New PlaceOrderParameters With {
                                    .EntryPrice = entry,
                                    .EntryDirection = Trade.TradeExecutionDirection.Buy,
                                    .Quantity = Me.LotSize,
                                    .Stoploss = stoploss,
                                    .Target = target,
                                    .Buffer = buffer,
                                    .SignalCandle = signalCandle,
                                    .OrderType = Trade.TypeOfOrder.SL,
                                    .Supporting1 = signalCandle.PayloadDate.ToString("HH:mm:ss"),
                                    .Supporting2 = targetRemark
                                }

                ElseIf signal.Item3 = Trade.TradeExecutionDirection.Sell Then
                    Dim buffer As Decimal = _parentStrategy.CalculateBuffer(signalCandle.Low, RoundOfType.Floor)
                    Dim entry As Decimal = signalCandle.Low - buffer
                    Dim stoploss As Decimal = signalCandle.High + buffer
                    Dim slTargetPoint As Decimal = ConvertFloorCeling(Math.Abs(stoploss - entry) * _userInputs.SLTargetMultiplier, _parentStrategy.TickSize, RoundOfType.Floor)
                    Dim atrTargetPoint As Decimal = ConvertFloorCeling(_atrPayloads(signalCandle.PayloadDate) * _userInputs.ATRTargetMultiplier, _parentStrategy.TickSize, RoundOfType.Floor)
                    Dim target As Decimal = Decimal.MinValue
                    Dim targetRemark As String = Nothing
                    If atrTargetPoint <= slTargetPoint Then
                        target = entry - atrTargetPoint
                        targetRemark = "ATR Target"
                    Else
                        target = entry - slTargetPoint
                        targetRemark = "SL Target"
                    End If
                    parameter = New PlaceOrderParameters With {
                                    .EntryPrice = entry,
                                    .EntryDirection = Trade.TradeExecutionDirection.Sell,
                                    .Quantity = Me.LotSize,
                                    .Stoploss = stoploss,
                                    .Target = target,
                                    .Buffer = buffer,
                                    .SignalCandle = signalCandle,
                                    .OrderType = Trade.TypeOfOrder.SL,
                                    .Supporting1 = signalCandle.PayloadDate.ToString("HH:mm:ss"),
                                    .Supporting2 = targetRemark
                                }

                End If
            End If
        End If
        If parameter IsNot Nothing Then
            ret = New Tuple(Of Boolean, List(Of PlaceOrderParameters))(True, New List(Of PlaceOrderParameters) From {parameter})
        End If
        Return ret
    End Function

    Public Overrides Async Function IsTriggerReceivedForExitOrderAsync(ByVal currentTick As Payload, ByVal currentTrade As Trade) As Task(Of Tuple(Of Boolean, String))
        Dim ret As Tuple(Of Boolean, String) = Nothing
        Await Task.Delay(0).ConfigureAwait(False)
        Dim currentMinuteCandlePayload As Payload = _signalPayload(_parentStrategy.GetCurrentXMinuteCandleTime(currentTick.PayloadDate, _signalPayload))
        If currentTrade IsNot Nothing Then
            If currentTrade.TradeCurrentStatus = Trade.TradeExecutionStatus.Open OrElse
                currentTrade.TradeCurrentStatus = Trade.TradeExecutionStatus.Inprogress Then
                Dim atrTrailingColor As Color = _atrTrailingColorPayloads(currentMinuteCandlePayload.PreviousCandlePayload.PayloadDate)
                If atrTrailingColor = Color.Green AndAlso currentTrade.EntryDirection = Trade.TradeExecutionDirection.Sell Then
                    ret = New Tuple(Of Boolean, String)(True, "Opposite Direction Signal")
                ElseIf atrTrailingColor = Color.Red AndAlso currentTrade.EntryDirection = Trade.TradeExecutionDirection.Buy Then
                    ret = New Tuple(Of Boolean, String)(True, "Opposite Direction Signal")
                End If
            End If
            If ret Is Nothing AndAlso currentTrade.TradeCurrentStatus = Trade.TradeExecutionStatus.Open Then
                Dim signal As Tuple(Of Boolean, Decimal, Trade.TradeExecutionDirection, Payload) = GetSignalCandle(currentMinuteCandlePayload.PreviousCandlePayload, currentTick)
                If signal IsNot Nothing Then
                    If signal.Item3 <> currentTrade.EntryDirection Or signal.Item4.PayloadDate <> currentTrade.SignalCandle.PayloadDate Then
                        ret = New Tuple(Of Boolean, String)(True, "Invalid Signal")
                    End If
                End If
            End If
        End If
        Return ret
    End Function

    Public Overrides Async Function IsTriggerReceivedForModifyStoplossOrderAsync(ByVal currentTick As Payload, ByVal currentTrade As Trade) As Task(Of Tuple(Of Boolean, Decimal, String))
        Dim ret As Tuple(Of Boolean, Decimal, String) = Nothing
        Await Task.Delay(0).ConfigureAwait(False)
        Return ret
    End Function

    Public Overrides Async Function IsTriggerReceivedForModifyTargetOrderAsync(ByVal currentTick As Payload, ByVal currentTrade As Trade) As Task(Of Tuple(Of Boolean, Decimal, String))
        Dim ret As Tuple(Of Boolean, Decimal, String) = Nothing
        Await Task.Delay(0).ConfigureAwait(False)
        Return ret
    End Function

    Public Overrides Function IsTriggerReceivedForExitCNCEODOrderAsync(currentTick As Payload, currentTrade As Trade) As Task(Of Tuple(Of Boolean, Decimal, String))
        Throw New NotImplementedException()
    End Function

    Private Function GetSignalCandle(ByVal candle As Payload, ByVal currentTick As Payload) As Tuple(Of Boolean, Decimal, Trade.TradeExecutionDirection, Payload)
        Dim ret As Tuple(Of Boolean, Decimal, Trade.TradeExecutionDirection, Payload) = Nothing
        If candle IsNot Nothing AndAlso candle.PreviousCandlePayload IsNot Nothing Then
            Dim hkCandle As Payload = _hkPayloads(candle.PayloadDate)
            Dim atrTrailingColor As Color = _atrTrailingColorPayloads(candle.PayloadDate)
            Dim lastTrade As Trade = _parentStrategy.GetLastTradeOfTheStock(candle, _parentStrategy.TradeType)
            If atrTrailingColor = Color.Green Then
                If hkCandle.Open = hkCandle.High Then
                    ret = New Tuple(Of Boolean, Decimal, Trade.TradeExecutionDirection, Payload)(True, hkCandle.High, Trade.TradeExecutionDirection.Buy, hkCandle)
                ElseIf lastTrade IsNot Nothing AndAlso lastTrade.EntryDirection = Trade.TradeExecutionDirection.Buy Then
                    ret = New Tuple(Of Boolean, Decimal, Trade.TradeExecutionDirection, Payload)(True, lastTrade.SignalCandle.High, Trade.TradeExecutionDirection.Buy, lastTrade.SignalCandle)
                End If
            ElseIf atrTrailingColor = Color.Red Then
                If hkCandle.Open = hkCandle.Low Then
                    ret = New Tuple(Of Boolean, Decimal, Trade.TradeExecutionDirection, Payload)(True, hkCandle.Low, Trade.TradeExecutionDirection.Sell, hkCandle)
                ElseIf lastTrade IsNot Nothing AndAlso lastTrade.EntryDirection = Trade.TradeExecutionDirection.Sell Then
                    ret = New Tuple(Of Boolean, Decimal, Trade.TradeExecutionDirection, Payload)(True, lastTrade.SignalCandle.Low, Trade.TradeExecutionDirection.Sell, lastTrade.SignalCandle)
                End If
            End If
        End If
        Return ret
    End Function
End Class