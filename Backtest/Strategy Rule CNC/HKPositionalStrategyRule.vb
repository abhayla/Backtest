﻿Imports Backtest.StrategyHelper
Imports System.Threading
Imports Algo2TradeBLL
Imports Utilities.Numbers.NumberManipulation

Public Class HKPositionalStrategyRule
    Inherits StrategyRule

    Private _hkPayload As Dictionary(Of Date, Payload)

    Private ReadOnly _stockSMAPercentage As Decimal
    Public Sub New(ByVal inputPayload As Dictionary(Of Date, Payload),
                   ByVal lotSize As Integer,
                   ByVal parentStrategy As Strategy,
                   ByVal tradingDate As Date,
                   ByVal tradingSymbol As String,
                   ByVal canceller As CancellationTokenSource,
                   ByVal entities As RuleEntities,
                   ByVal stockSMAPer As Decimal)
        MyBase.New(inputPayload, lotSize, parentStrategy, tradingDate, tradingSymbol, canceller, entities)
        _stockSMAPercentage = stockSMAPer
    End Sub

    Public Overrides Sub CompletePreProcessing()
        MyBase.CompletePreProcessing()

        Indicator.HeikenAshi.ConvertToHeikenAshi(_signalPayload, _hkPayload)
    End Sub

    Public Overrides Async Function IsTriggerReceivedForPlaceOrderAsync(currentTick As Payload) As Task(Of Tuple(Of Boolean, List(Of PlaceOrderParameters)))
        Dim ret As Tuple(Of Boolean, List(Of PlaceOrderParameters)) = Nothing
        Await Task.Delay(0).ConfigureAwait(False)
        Dim currentMinuteCandlePayload As Payload = _signalPayload(_parentStrategy.GetCurrentXMinuteCandleTime(currentTick.PayloadDate, _signalPayload))
        Dim tradeStartTime As Date = New Date(_tradingDate.Year, _tradingDate.Month, _tradingDate.Day, _parentStrategy.TradeStartTime.Hours, _parentStrategy.TradeStartTime.Minutes, _parentStrategy.TradeStartTime.Seconds)

        Dim parameter As PlaceOrderParameters = Nothing
        If currentMinuteCandlePayload IsNot Nothing AndAlso currentMinuteCandlePayload.PreviousCandlePayload IsNot Nothing AndAlso
            Not _parentStrategy.IsTradeOpen(currentTick, _parentStrategy.TradeType) Then
            Dim signalCandle As Payload = Nothing

            Dim signalReceivedForEntry As Tuple(Of Boolean, Decimal, Payload) = GetSignalForEntry(currentTick)
            If signalReceivedForEntry IsNot Nothing AndAlso signalReceivedForEntry.Item1 Then
                signalCandle = signalReceivedForEntry.Item3

                If signalCandle IsNot Nothing Then
                    Dim highestEntryPrice As Decimal = Decimal.MinValue
                    Dim lastExecutedTrade As Trade = _parentStrategy.GetLastExecutedTradeOfTheStock(currentTick, _parentStrategy.TradeType)
                    If lastExecutedTrade IsNot Nothing Then highestEntryPrice = lastExecutedTrade.Supporting1
                    Dim quantity As Integer = 1
                    If highestEntryPrice > signalReceivedForEntry.Item2 Then
                        quantity = 2
                    End If
                    highestEntryPrice = Math.Max(highestEntryPrice, parameter.EntryPrice)

                    parameter = New PlaceOrderParameters With {
                        .EntryPrice = signalReceivedForEntry.Item2,
                        .EntryDirection = Trade.TradeExecutionDirection.Buy,
                        .Quantity = quantity,
                        .Stoploss = .EntryPrice - 1000000,
                        .Target = .EntryPrice + 1000000,
                        .Buffer = 0,
                        .SignalCandle = signalCandle,
                        .OrderType = Trade.TypeOfOrder.Market,
                        .Supporting1 = highestEntryPrice
                    }

                End If
            End If
        End If
        If parameter IsNot Nothing Then
            ret = New Tuple(Of Boolean, List(Of PlaceOrderParameters))(True, New List(Of PlaceOrderParameters) From {parameter})
        End If
        Return ret
    End Function

    Public Overrides Function IsTriggerReceivedForExitOrderAsync(currentTick As Payload, currentTrade As Trade) As Task(Of Tuple(Of Boolean, String))
        Throw New NotImplementedException()
    End Function

    Public Overrides Async Function IsTriggerReceivedForExitCNCEODOrderAsync(currentTick As Payload, currentTrade As Trade) As Task(Of Tuple(Of Boolean, Decimal, String))
        Dim ret As Tuple(Of Boolean, Decimal, String) = Nothing
        Await Task.Delay(0).ConfigureAwait(False)
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

#Region "Entry Rule"
    Private Function GetSignalForEntry(ByVal currentTick As Payload) As Tuple(Of Boolean, Decimal, Payload)
        Dim ret As Tuple(Of Boolean, Decimal, Payload) = Nothing
        If _hkPayload IsNot Nothing AndAlso _hkPayload.Count > 0 AndAlso _hkPayload.ContainsKey(currentTick.PayloadDate.Date) Then
            Dim currentDayPayload As Payload = _hkPayload(currentTick.PayloadDate.Date)
            Dim lastEntrySignal As Payload = GetLastEntrySignal(currentDayPayload)
            Dim entryPrice As Decimal = ConvertFloorCeling(lastEntrySignal.High, Me._parentStrategy.TickSize, RoundOfType.Floor)
            If IsEntrySignalStillValid(lastEntrySignal, currentDayPayload) AndAlso currentDayPayload.High >= entryPrice Then
                ret = New Tuple(Of Boolean, Decimal, Payload)(True, entryPrice, lastEntrySignal)
            End If
        End If
        Return ret
    End Function

    Private Function GetLastEntrySignal(ByVal currentDayPayload As Payload) As Payload
        Dim ret As Payload = Nothing
        For Each runningPayload In _hkPayload.OrderByDescending(Function(x)
                                                                    Return x.Key
                                                                End Function)
            If runningPayload.Key < currentDayPayload.PayloadDate Then
                If runningPayload.Value.CandleStrengthHeikenAshi = Payload.StrongCandle.Bearish Then
                    ret = runningPayload.Value
                    Exit For
                End If
            End If
        Next
        Return ret
    End Function

    Private Function IsEntrySignalStillValid(ByVal signalPayload As Payload, ByVal currentDayPayload As Payload) As Boolean
        Dim ret As Boolean = True
        Dim timeToCheckPayload As IEnumerable(Of KeyValuePair(Of Date, Payload)) = _hkPayload.Where(Function(x)
                                                                                                        Return x.Key > signalPayload.PayloadDate AndAlso
                                                                                                 x.Key < currentDayPayload.PayloadDate
                                                                                                    End Function)
        If timeToCheckPayload IsNot Nothing AndAlso timeToCheckPayload.Count > 0 Then
            For Each runningPayload In timeToCheckPayload
                Dim entryPoint As Decimal = ConvertFloorCeling(signalPayload.High, Me._parentStrategy.TickSize, RoundOfType.Floor)
                If runningPayload.Value.High >= entryPoint Then
                    ret = False
                    Exit For
                End If
            Next
        End If
        Return ret
    End Function
#End Region

End Class
