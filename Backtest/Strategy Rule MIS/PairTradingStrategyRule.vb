﻿Imports Backtest.StrategyHelper
Imports System.Threading
Imports Algo2TradeBLL
Imports Utilities.Numbers.NumberManipulation

Public Class PairTradingStrategyRule
    Inherits StrategyRule

#Region "Entity"
    Public Class StrategyRuleEntities
        Inherits RuleEntities

        Public TargetMultiplier As Decimal
    End Class
#End Region

    Private _ATRPayload As Dictionary(Of Date, Decimal)

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
        Indicator.ATR.CalculateATR(14, _signalPayload, _ATRPayload)
    End Sub

    Public Overrides Async Function IsTriggerReceivedForPlaceOrderAsync(currentTick As Payload) As Task(Of Tuple(Of Boolean, List(Of PlaceOrderParameters)))
        Dim ret As Tuple(Of Boolean, List(Of PlaceOrderParameters)) = Nothing
        Await Task.Delay(0).ConfigureAwait(False)
        Dim currentMinuteCandlePayload As Payload = _signalPayload(_parentStrategy.GetCurrentXMinuteCandleTime(currentTick.PayloadDate, _signalPayload))
        Dim tradeStartTime As Date = New Date(_tradingDate.Year, _tradingDate.Month, _tradingDate.Day, _parentStrategy.TradeStartTime.Hours, _parentStrategy.TradeStartTime.Minutes, _parentStrategy.TradeStartTime.Seconds)

        Dim parameters As List(Of PlaceOrderParameters) = Nothing
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
            Dim lastExecutedTrade As Trade = _parentStrategy.GetLastExecutedTradeOfTheStock(currentMinuteCandlePayload, _parentStrategy.TradeType)
            If lastExecutedTrade Is Nothing Then
                signalCandle = currentMinuteCandlePayload
            Else
                If Not IsLastTradeExitedAtCurrentCandle(currentMinuteCandlePayload) Then
                    signalCandle = currentMinuteCandlePayload
                End If
            End If

            If signalCandle IsNot Nothing Then
                Dim slPoint As Decimal = ConvertFloorCeling(GetHighestATR(signalCandle), _parentStrategy.TickSize, RoundOfType.Celing)
                Dim slPL As Decimal = Me._parentStrategy.CalculatePL(_tradingSymbol, currentTick.Open, currentTick.Open - slPoint, _lotSize, _lotSize, Me._parentStrategy.StockType)
                Dim targetPrice As Decimal = Me._parentStrategy.CalculatorTargetOrStoploss(_tradingSymbol, currentTick.Open, _lotSize / _lotSize, Math.Abs(slPL) * _userInputs.TargetMultiplier, Trade.TradeExecutionDirection.Buy, Me._parentStrategy.StockType)
                Dim targetPoint As Decimal = targetPrice - currentTick.Open

                Dim buffer As Decimal = _parentStrategy.CalculateBuffer(currentTick.Open, RoundOfType.Floor)
                Dim parameter1 As PlaceOrderParameters = New PlaceOrderParameters With {
                                                        .EntryPrice = currentTick.Open,
                                                        .EntryDirection = Trade.TradeExecutionDirection.Buy,
                                                        .Quantity = _lotSize,
                                                        .Stoploss = .EntryPrice - slPoint,
                                                        .Target = .EntryPrice + targetPoint,
                                                        .Buffer = buffer,
                                                        .SignalCandle = signalCandle,
                                                        .OrderType = Trade.TypeOfOrder.Market,
                                                        .Supporting1 = signalCandle.PayloadDate.ToString("HH:mm:ss"),
                                                        .Supporting2 = slPoint
                                                    }

                Dim parameter2 As PlaceOrderParameters = New PlaceOrderParameters With {
                                                        .EntryPrice = currentTick.Open,
                                                        .EntryDirection = Trade.TradeExecutionDirection.Sell,
                                                        .Quantity = _lotSize,
                                                        .Stoploss = .EntryPrice + slPoint,
                                                        .Target = .EntryPrice - targetPoint,
                                                        .Buffer = buffer,
                                                        .SignalCandle = signalCandle,
                                                        .OrderType = Trade.TypeOfOrder.Market,
                                                        .Supporting1 = signalCandle.PayloadDate.ToString("HH:mm:ss"),
                                                        .Supporting2 = slPoint
                                                    }

                parameters = New List(Of PlaceOrderParameters) From {parameter1, parameter2}
            End If
        End If
        If parameters IsNot Nothing AndAlso parameters.Count = 2 Then
            ret = New Tuple(Of Boolean, List(Of PlaceOrderParameters))(True, parameters)

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
        'Dim currentMinuteCandlePayload As Payload = _signalPayload(_parentStrategy.GetCurrentXMinuteCandleTime(currentTick.PayloadDate, _signalPayload))
        'If currentTrade IsNot Nothing AndAlso currentTrade.TradeCurrentStatus = Trade.TradeExecutionStatus.Inprogress Then

        'End If
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

    Private Function GetLastOrderExitTime(ByVal currentCandle As Payload) As Date
        Dim ret As Date = Date.MinValue
        Dim lastExecutedOrder As Trade = Me._parentStrategy.GetLastExecutedTradeOfTheStock(currentCandle, Me._parentStrategy.TradeType)
        If lastExecutedOrder IsNot Nothing Then
            ret = lastExecutedOrder.ExitTime
        End If
        Return ret
    End Function

    Private Function IsLastTradeExitedAtCurrentCandle(ByVal currentCandle As Payload) As Boolean
        Dim ret As Boolean = False
        Dim lastTradeExitTime As Date = GetLastOrderExitTime(currentCandle)
        If lastTradeExitTime <> Date.MinValue Then
            Dim blockDateInThisTimeframe As Date = Date.MinValue
            Dim timeframe As Integer = Me._parentStrategy.SignalTimeFrame
            If Me._parentStrategy.ExchangeStartTime.Minutes Mod timeframe = 0 Then
                blockDateInThisTimeframe = New Date(lastTradeExitTime.Year,
                                                    lastTradeExitTime.Month,
                                                    lastTradeExitTime.Day,
                                                    lastTradeExitTime.Hour,
                                                    Math.Floor(lastTradeExitTime.Minute / timeframe) * timeframe, 0)
            Else
                Dim exchangeStartTime As Date = New Date(lastTradeExitTime.Year, lastTradeExitTime.Month, lastTradeExitTime.Day, Me._parentStrategy.ExchangeStartTime.Hours, Me._parentStrategy.ExchangeStartTime.Minutes, 0)
                Dim currentTime As Date = New Date(lastTradeExitTime.Year, lastTradeExitTime.Month, lastTradeExitTime.Day, lastTradeExitTime.Hour, lastTradeExitTime.Minute, 0)
                Dim timeDifference As Double = currentTime.Subtract(exchangeStartTime).TotalMinutes
                Dim adjustedTimeDifference As Integer = Math.Floor(timeDifference / timeframe) * timeframe
                Dim currentMinute As Date = exchangeStartTime.AddMinutes(adjustedTimeDifference)
                blockDateInThisTimeframe = New Date(lastTradeExitTime.Year, lastTradeExitTime.Month, lastTradeExitTime.Day, currentMinute.Hour, currentMinute.Minute, 0)
            End If
            If blockDateInThisTimeframe <> Date.MinValue Then
                ret = Utilities.Time.IsDateTimeEqualTillMinutes(blockDateInThisTimeframe, currentCandle.PayloadDate)
            End If
        End If
        Return ret
    End Function

    Private Function GetHighestATR(ByVal currentCandle As Payload) As Decimal
        Dim ret As Decimal = Decimal.MinValue
        If _ATRPayload IsNot Nothing AndAlso _ATRPayload.Count > 0 Then
            ret = _ATRPayload.Max(Function(x)
                                      If x.Key.Date = _tradingDate.Date AndAlso x.Key < currentCandle.PayloadDate Then
                                          Return x.Value
                                      Else
                                          Return Decimal.MinValue
                                      End If
                                  End Function)
        End If
        Return ret
    End Function
End Class