﻿Imports Algo2TradeBLL
Imports System.IO
Imports System.Threading
Imports Utilities.Strings

Namespace StrategyHelper
    Public Class MISGenericStrategy
        Inherits Strategy
        Implements IDisposable
        Public Property StockFileName As String
        Public Property RuleNumber As Integer
        Public Property RuleEntityData As RuleEntities
        Public Sub New(ByVal canceller As CancellationTokenSource,
                       ByVal exchangeStartTime As TimeSpan,
                       ByVal exchangeEndTime As TimeSpan,
                       ByVal tradeStartTime As TimeSpan,
                       ByVal lastTradeEntryTime As TimeSpan,
                       ByVal eodExitTime As TimeSpan,
                       ByVal tickSize As Decimal,
                       ByVal marginMultiplier As Decimal,
                       ByVal timeframe As Integer,
                       ByVal heikenAshiCandle As Boolean,
                       ByVal stockType As Trade.TypeOfStock,
                       ByVal databaseTable As Common.DataBaseTable,
                       ByVal dataSource As SourceOfData,
                       ByVal initialCapital As Decimal,
                       ByVal usableCapital As Decimal,
                       ByVal minimumEarnedCapitalToWithdraw As Decimal,
                       ByVal amountToBeWithdrawn As Decimal)
            MyBase.New(canceller, exchangeStartTime, exchangeEndTime, tradeStartTime, lastTradeEntryTime, eodExitTime, tickSize, marginMultiplier, timeframe, heikenAshiCandle, stockType, Trade.TypeOfTrade.MIS, databaseTable, dataSource, initialCapital, usableCapital, minimumEarnedCapitalToWithdraw, amountToBeWithdrawn)
            Me.NumberOfTradesPerDay = Integer.MaxValue
        End Sub
        Public Overrides Async Function TestStrategyAsync(startDate As Date, endDate As Date, filename As String) As Task
            If Not Me.ExitOnOverAllFixedTargetStoploss Then
                Me.OverAllProfitPerDay = Decimal.MaxValue
                Me.OverAllLossPerDay = Decimal.MinValue
            End If
            If Not Me.ExitOnStockFixedTargetStoploss Then
                Me.StockMaxProfitPerDay = Decimal.MaxValue
                Me.StockMaxLossPerDay = Decimal.MinValue
            End If

            If filename Is Nothing Then Throw New ApplicationException("Invalid Filename")
            Dim tradesFileName As String = Path.Combine(My.Application.Info.DirectoryPath, String.Format("{0}.Trades.a2t", filename))
            Dim capitalFileName As String = Path.Combine(My.Application.Info.DirectoryPath, String.Format("{0}.Capital.a2t", filename))

            If File.Exists(tradesFileName) AndAlso File.Exists(capitalFileName) Then
                Dim folderpath As String = Path.Combine(My.Application.Info.DirectoryPath, "BackTest Output")
                Dim files() = Directory.GetFiles(folderpath, "*.xlsx")
                For Each file In files
                    If file.ToUpper.Contains(filename.ToUpper) Then
                        Exit Function
                    End If
                Next
                PrintArrayToExcel(filename, tradesFileName, capitalFileName)
            Else
                If File.Exists(tradesFileName) Then File.Delete(tradesFileName)
                If File.Exists(capitalFileName) Then File.Delete(capitalFileName)
                Dim totalPL As Decimal = 0
                Dim tradeCheckingDate As Date = startDate.Date
                Dim portfolioLossPerDay As Decimal = Me.OverAllLossPerDay
                While tradeCheckingDate <= endDate.Date
                    _canceller.Token.ThrowIfCancellationRequested()
                    Me.AvailableCapital = Me.UsableCapital
                    Me.OverAllLossPerDay = portfolioLossPerDay
                    TradesTaken = New Dictionary(Of Date, Dictionary(Of String, List(Of Trade)))
                    Dim stockList As Dictionary(Of String, StockDetails) = Await GetStockData(tradeCheckingDate).ConfigureAwait(False)

                    _canceller.Token.ThrowIfCancellationRequested()
                    If stockList IsNot Nothing AndAlso stockList.Count > 0 Then
                        Dim currentDayOneMinuteStocksPayload As Dictionary(Of String, Dictionary(Of Date, Payload)) = Nothing
                        Dim XDayOneMinuteStocksPayload As Dictionary(Of String, Dictionary(Of Date, Payload)) = Nothing
                        Dim stocksRuleData As Dictionary(Of String, StrategyRule) = Nothing

                        'First lets build the payload for all the stocks
                        Dim stockCount As Integer = 0
                        Dim eligibleStockCount As Integer = 0
                        For Each stock In stockList.Keys
                            _canceller.Token.ThrowIfCancellationRequested()
                            stockCount += 1
                            Dim XDayOneMinutePayload As Dictionary(Of Date, Payload) = Nothing
                            Dim currentDayOneMinutePayload As Dictionary(Of Date, Payload) = Nothing
                            If Me.DataSource = SourceOfData.Database Then
                                XDayOneMinutePayload = Cmn.GetRawPayload(Me.DatabaseTable, stock, tradeCheckingDate.AddDays(-7), tradeCheckingDate)
                            ElseIf Me.DataSource = SourceOfData.Live Then
                                XDayOneMinutePayload = Await Cmn.GetHistoricalDataAsync(Me.DatabaseTable, stock, tradeCheckingDate.AddDays(-7), tradeCheckingDate).ConfigureAwait(False)
                            End If

                            _canceller.Token.ThrowIfCancellationRequested()
                            'Now transfer only the current date payload into the workable payload (this will be used for the main loop and checking if the date is a valid date)
                            If XDayOneMinutePayload IsNot Nothing AndAlso XDayOneMinutePayload.Count > 0 Then
                                OnHeartbeat(String.Format("Processing for {0} on {1}. Stock Counter: [ {2}/{3} ]", stock, tradeCheckingDate.ToShortDateString, stockCount, stockList.Count))
                                For Each runningPayload In XDayOneMinutePayload.Keys
                                    _canceller.Token.ThrowIfCancellationRequested()
                                    If runningPayload.Date = tradeCheckingDate.Date Then
                                        If currentDayOneMinutePayload Is Nothing Then currentDayOneMinutePayload = New Dictionary(Of Date, Payload)
                                        currentDayOneMinutePayload.Add(runningPayload, XDayOneMinutePayload(runningPayload))
                                    End If
                                Next
                                'Add all these payloads into the stock collections
                                If currentDayOneMinutePayload IsNot Nothing AndAlso currentDayOneMinutePayload.Count > 0 Then
                                    If currentDayOneMinuteStocksPayload Is Nothing Then currentDayOneMinuteStocksPayload = New Dictionary(Of String, Dictionary(Of Date, Payload))
                                    currentDayOneMinuteStocksPayload.Add(stock, currentDayOneMinutePayload)
                                    If XDayOneMinuteStocksPayload Is Nothing Then XDayOneMinuteStocksPayload = New Dictionary(Of String, Dictionary(Of Date, Payload))
                                    XDayOneMinuteStocksPayload.Add(stock, XDayOneMinutePayload)
                                    If stocksRuleData Is Nothing Then stocksRuleData = New Dictionary(Of String, StrategyRule)
                                    Dim stockRule As StrategyRule = Nothing

                                    Dim tradingSymbol As String = currentDayOneMinutePayload.LastOrDefault.Value.TradingSymbol
                                    Select Case RuleNumber
                                        Case 0
                                            stockRule = New SmallestCandleBreakoutStrategyRule(XDayOneMinutePayload, stockList(stock).LotSize, Me, tradeCheckingDate, tradingSymbol, _canceller, RuleEntityData)
                                        Case 1
                                            stockRule = New HighVolumePinBarStrategyRule(XDayOneMinutePayload, stockList(stock).LotSize, Me, tradeCheckingDate, tradingSymbol, _canceller, RuleEntityData)
                                        Case 2
                                            stockRule = New MomentumReversalv2StrategyRule(XDayOneMinutePayload, stockList(stock).LotSize, Me, tradeCheckingDate, tradingSymbol, _canceller, RuleEntityData)
                                        Case 3
                                            stockRule = New HighVolumePinBarv2StrategyRule(XDayOneMinutePayload, stockList(stock).LotSize, Me, tradeCheckingDate, tradingSymbol, _canceller, RuleEntityData)
                                        Case 4
                                            stockRule = New DonchianFractalStrategyRule(XDayOneMinutePayload, stockList(stock).LotSize, Me, tradeCheckingDate, tradingSymbol, _canceller, RuleEntityData)
                                        Case 5
                                            stockRule = New SMIFractalStrategyRule(XDayOneMinutePayload, stockList(stock).LotSize, Me, tradeCheckingDate, tradingSymbol, _canceller, RuleEntityData)
                                        Case 6
                                            stockRule = New BANKNIFTYDayLongSMIStrategyRule(XDayOneMinutePayload, stockList(stock).LotSize, Me, tradeCheckingDate, tradingSymbol, _canceller, RuleEntityData)
                                        Case 7
                                            stockRule = New DayStartSMIStrategyRule(XDayOneMinutePayload, stockList(stock).LotSize, Me, tradeCheckingDate, tradingSymbol, _canceller, RuleEntityData)
                                        Case 8
                                            stockRule = New GapFractalBreakoutStrategyRule(XDayOneMinutePayload, stockList(stock).LotSize, Me, tradeCheckingDate, tradingSymbol, _canceller, RuleEntityData, stockList(stock).Supporting1)
                                        Case 9
                                            stockRule = New ForwardMomentumv2StrategyRule(XDayOneMinutePayload, stockList(stock).LotSize, Me, tradeCheckingDate, tradingSymbol, _canceller, RuleEntityData)
                                        Case 10
                                            stockRule = New VijayCNCStrategyRule(XDayOneMinutePayload, stockList(stock).LotSize, Me, tradeCheckingDate, tradingSymbol, _canceller, RuleEntityData)
                                        Case 11
                                            stockRule = New TIIOppositeBreakoutStrategyRule(XDayOneMinutePayload, stockList(stock).LotSize, Me, tradeCheckingDate, tradingSymbol, _canceller, RuleEntityData)
                                        Case 12
                                            stockRule = New FixedLevelBasedStrategyRule(XDayOneMinutePayload, stockList(stock).LotSize, Me, tradeCheckingDate, tradingSymbol, _canceller, RuleEntityData, stockList(stock).Supporting1)
                                        Case 13
                                            stockRule = New LowStoplossStrategyRule(XDayOneMinutePayload, stockList(stock).LotSize, Me, tradeCheckingDate, tradingSymbol, _canceller, RuleEntityData, stockList(stock).Supporting1, stockList(stock).Supporting2, stockList(stock).Supporting3, stockList(stock).Supporting4)
                                        Case 14
                                            stockRule = New MultiTargetStrategyRule(XDayOneMinutePayload, stockList(stock).LotSize, Me, tradeCheckingDate, tradingSymbol, _canceller, RuleEntityData)
                                        Case 15
                                            stockRule = New ReversalStrategyRule(XDayOneMinutePayload, stockList(stock).LotSize, Me, tradeCheckingDate, tradingSymbol, _canceller, RuleEntityData, stockList(stock).Supporting2)
                                        Case 16
                                            stockRule = New PinbarBreakoutStrategyRule(XDayOneMinutePayload, stockList(stock).LotSize, Me, tradeCheckingDate, tradingSymbol, _canceller, RuleEntityData, stockList(stock).Supporting2)
                                        Case 17
                                            stockRule = New LowSLPinbarStrategyRule(XDayOneMinutePayload, stockList(stock).LotSize, Me, tradeCheckingDate, tradingSymbol, _canceller, RuleEntityData, stockList(stock).Supporting1, stockList(stock).Supporting2, stockList(stock).Supporting3, stockList(stock).Supporting4)
                                        Case 18
                                            Throw New ApplicationException("Not a MIS strategy")
                                        Case 19
                                            stockRule = New LowStoplossWickStrategyRule(XDayOneMinutePayload, stockList(stock).LotSize, Me, tradeCheckingDate, tradingSymbol, _canceller, RuleEntityData)
                                        Case 20
                                            stockRule = New LowStoplossCandleStrategyRule(XDayOneMinutePayload, stockList(stock).LotSize, Me, tradeCheckingDate, tradingSymbol, _canceller, RuleEntityData)
                                        Case 21
                                            stockRule = New PairTradingStrategyRule(XDayOneMinutePayload, stockList(stock).LotSize, Me, tradeCheckingDate, tradingSymbol, _canceller, RuleEntityData)
                                        Case 22
                                            stockRule = New CoinFlipAtResistanceStrategyRule(XDayOneMinutePayload, stockList(stock).LotSize, Me, tradeCheckingDate, tradingSymbol, _canceller, RuleEntityData)
                                        Case 30
                                            stockRule = New PivotsPointsStrategyRule(XDayOneMinutePayload, stockList(stock).LotSize, Me, tradeCheckingDate, tradingSymbol, _canceller, RuleEntityData)
                                        Case 32
                                            stockRule = New IntradayPositionalStrategyRule(XDayOneMinutePayload, stockList(stock).LotSize, Me, tradeCheckingDate, tradingSymbol, _canceller, RuleEntityData, stockList(stock).Supporting1, stockList(stock).Supporting2)
                                        Case 33
                                            stockRule = New DoubleTopDoubleBottomStrategyRule(XDayOneMinutePayload, stockList(stock).LotSize, Me, tradeCheckingDate, tradingSymbol, _canceller, RuleEntityData)
                                        Case 35
                                            stockRule = New IntradayPositionalStrategyRule2(XDayOneMinutePayload, stockList(stock).LotSize, Me, tradeCheckingDate, tradingSymbol, _canceller, RuleEntityData)
                                        Case 36
                                            stockRule = New IntradayPositionalStrategyRule3(XDayOneMinutePayload, stockList(stock).LotSize, Me, tradeCheckingDate, tradingSymbol, _canceller, RuleEntityData, stockList(stock).Supporting1)
                                        Case 37
                                            stockRule = New IntradayPositionalStrategyRule4(XDayOneMinutePayload, stockList(stock).LotSize, Me, tradeCheckingDate, tradingSymbol, _canceller, RuleEntityData)
                                        Case 39
                                            stockRule = New NiftyBankMarketPairTradingStrategyRule(XDayOneMinutePayload, stockList(stock).LotSize, Me, tradeCheckingDate, tradingSymbol, _canceller, RuleEntityData, stockList(stock).Supporting1)
                                        Case 40
                                            stockRule = New FavourableFractalBreakoutStrategyRule(XDayOneMinutePayload, stockList(stock).LotSize, Me, tradeCheckingDate, tradingSymbol, _canceller, RuleEntityData)
                                        Case 41
                                            stockRule = New FractalDipStrategyRule(XDayOneMinutePayload, stockList(stock).LotSize, Me, tradeCheckingDate, tradingSymbol, _canceller, RuleEntityData)
                                        Case 42
                                            stockRule = New CRUDEOIL_EODStrategyRule(XDayOneMinutePayload, stockList(stock).LotSize, Me, tradeCheckingDate, tradingSymbol, _canceller, RuleEntityData)
                                        Case 44
                                            stockRule = New OutsideBollingerStrategyRule(XDayOneMinutePayload, stockList(stock).LotSize, Me, tradeCheckingDate, tradingSymbol, _canceller, RuleEntityData)
                                        Case 45
                                            stockRule = New FavourableFractalBreakoutStrategyRule2(XDayOneMinutePayload, stockList(stock).LotSize, Me, tradeCheckingDate, tradingSymbol, _canceller, RuleEntityData)
                                        Case 49
                                            stockRule = New HKATRTraillingStrategyRule(XDayOneMinutePayload, stockList(stock).LotSize, Me, tradeCheckingDate, tradingSymbol, _canceller, RuleEntityData)
                                        Case 52
                                            stockRule = New HKSlabBasedStrategyRule(XDayOneMinutePayload, stockList(stock).LotSize, Me, tradeCheckingDate, tradingSymbol, _canceller, RuleEntityData, stockList(stock).Supporting1)
                                        Case 53
                                            stockRule = New HighLowSlabLevelBasedStrategyRule(XDayOneMinutePayload, stockList(stock).LotSize, Me, tradeCheckingDate, tradingSymbol, _canceller, RuleEntityData, stockList(stock).Supporting1)
                                        Case 54
                                            stockRule = New PreviousDayFactorStrategyRule(XDayOneMinutePayload, stockList(stock).LotSize, Me, tradeCheckingDate, tradingSymbol, _canceller, RuleEntityData)
                                        Case 55
                                            stockRule = New HighLowEMAStrategyRule(XDayOneMinutePayload, stockList(stock).LotSize, Me, tradeCheckingDate, tradingSymbol, _canceller, RuleEntityData)
                                    End Select

                                    AddHandler stockRule.Heartbeat, AddressOf OnHeartbeat
                                    stockRule.CompletePreProcessing()
                                    stocksRuleData.Add(stock, stockRule)
                                End If
                            End If
                        Next
                        '---------------------------------------------------------------------------------------------------------------------------------------------------------------------------

                        If currentDayOneMinuteStocksPayload IsNot Nothing AndAlso currentDayOneMinuteStocksPayload.Count > 0 Then
                            OnHeartbeat(String.Format("Checking Trade on {0}", tradeCheckingDate.ToShortDateString))
                            _canceller.Token.ThrowIfCancellationRequested()
                            Dim startMinute As TimeSpan = Me.ExchangeStartTime
                            Dim endMinute As TimeSpan = ExchangeEndTime
                            While startMinute < endMinute
                                _canceller.Token.ThrowIfCancellationRequested()
                                OnHeartbeat(String.Format("Checking Trade on {0}. Time:{1}", tradeCheckingDate.ToShortDateString, startMinute.ToString))
                                Dim startSecond As TimeSpan = startMinute
                                Dim endSecond As TimeSpan = startMinute.Add(TimeSpan.FromMinutes(Me.SignalTimeFrame - 1))
                                endSecond = endSecond.Add(TimeSpan.FromSeconds(59))
                                Dim potentialCandleSignalTime As Date = New Date(tradeCheckingDate.Year, tradeCheckingDate.Month, tradeCheckingDate.Day, startMinute.Hours, startMinute.Minutes, startMinute.Seconds)
                                Dim potentialTickSignalTime As Date = Nothing

                                _canceller.Token.ThrowIfCancellationRequested()
                                While startSecond <= endSecond
                                    potentialTickSignalTime = New Date(tradeCheckingDate.Year, tradeCheckingDate.Month, tradeCheckingDate.Day, startSecond.Hours, startSecond.Minutes, startSecond.Seconds)
                                    If potentialTickSignalTime.Second = 0 Then
                                        potentialCandleSignalTime = potentialTickSignalTime
                                        For Each stockName In stockList
                                            stockName.Value.PlaceOrderDoneForTheMinute = False
                                            stockName.Value.ExitOrderDoneForTheMinute = False
                                            stockName.Value.CancelOrderDoneForTheMinute = False
                                            stockName.Value.ModifyStoplossOrderDoneForTheMinute = False
                                            stockName.Value.ModifyTargetOrderDoneForTheMinute = False
                                        Next
                                    End If
                                    For Each stockName In stockList.Keys
                                        _canceller.Token.ThrowIfCancellationRequested()

                                        If Not stocksRuleData.ContainsKey(stockName) Then
                                            Continue For
                                        End If
                                        Dim stockStrategyRule As StrategyRule = stocksRuleData(stockName)

                                        If Not stockList(stockName).EligibleToTakeTrade Then
                                            Continue For
                                        End If

                                        If Not currentDayOneMinuteStocksPayload.ContainsKey(stockName) Then
                                            Continue For
                                        End If

                                        'Get the current minute candle from the stock collection for this stock for that day
                                        _canceller.Token.ThrowIfCancellationRequested()
                                        Dim currentMinuteCandlePayload As Payload = Nothing
                                        If currentDayOneMinuteStocksPayload.ContainsKey(stockName) AndAlso
                                            currentDayOneMinuteStocksPayload(stockName).ContainsKey(potentialCandleSignalTime) Then
                                            currentMinuteCandlePayload = currentDayOneMinuteStocksPayload(stockName)(potentialCandleSignalTime)
                                        End If

                                        'Now get the ticks for this minute and second
                                        _canceller.Token.ThrowIfCancellationRequested()
                                        Dim currentSecondTickPayload As List(Of Payload) = Nothing
                                        If currentMinuteCandlePayload IsNot Nothing AndAlso currentMinuteCandlePayload.Ticks IsNot Nothing Then
                                            currentSecondTickPayload = currentMinuteCandlePayload.Ticks.FindAll(Function(x)
                                                                                                                    Return x.PayloadDate = potentialTickSignalTime
                                                                                                                End Function)

                                            _canceller.Token.ThrowIfCancellationRequested()
                                            If currentSecondTickPayload IsNot Nothing AndAlso currentSecondTickPayload.Count > 0 Then
                                                For Each runningTick In currentSecondTickPayload
                                                    _canceller.Token.ThrowIfCancellationRequested()
                                                    SetCurrentLTPForStock(currentMinuteCandlePayload, runningTick, Trade.TypeOfTrade.MIS)

                                                    'Update Collection
                                                    Await stockStrategyRule.UpdateRequiredCollectionsAsync(runningTick).ConfigureAwait(False)

                                                    'Set Overall MTM
                                                    If Me.TypeOfMTMTrailing = MTMTrailingType.FixedSlabTrailing Then
                                                        Me.ExitOnOverAllFixedTargetStoploss = True
                                                        Dim trailingMTMLoss As Decimal = CalculateTrailingMTM(Me.MTMSlab, Me.MovementSlab, TotalPLAfterBrokerage(tradeCheckingDate))
                                                        If trailingMTMLoss <> Decimal.MinValue AndAlso trailingMTMLoss > Me.OverAllLossPerDay Then
                                                            Me.OverAllLossPerDay = trailingMTMLoss
                                                        End If
                                                    ElseIf Me.TypeOfMTMTrailing = MTMTrailingType.LogSlabTrailing Then
                                                        Me.ExitOnOverAllFixedTargetStoploss = True
                                                        Dim trailingMTMLoss As Decimal = CalculateLogTrailingMTM(Me.MTMSlab, TotalPLAfterBrokerage(tradeCheckingDate))
                                                        If trailingMTMLoss <> Decimal.MinValue AndAlso trailingMTMLoss > Me.OverAllLossPerDay Then
                                                            Me.OverAllLossPerDay = trailingMTMLoss
                                                        End If
                                                    ElseIf Me.TypeOfMTMTrailing = MTMTrailingType.RealtimeTrailing Then
                                                        If portfolioLossPerDay <> Decimal.MinValue AndAlso
                                                            TotalPLAfterBrokerage(tradeCheckingDate) >= Math.Abs(portfolioLossPerDay) Then
                                                            Me.ExitOnOverAllFixedTargetStoploss = True
                                                            Dim trailingMTMLoss As Decimal = TotalPLAfterBrokerage(tradeCheckingDate) * Me.RealtimeTrailingPercentage / 100
                                                            If trailingMTMLoss <> Decimal.MinValue AndAlso trailingMTMLoss > Me.OverAllLossPerDay Then
                                                                Me.OverAllLossPerDay = trailingMTMLoss
                                                            End If
                                                        End If
                                                    End If

                                                    'Specific Stock MTM Check
                                                    _canceller.Token.ThrowIfCancellationRequested()
                                                    If StockPLAfterBrokerage(tradeCheckingDate, runningTick.TradingSymbol) >= stockStrategyRule.MaxProfitOfThisStock Then
                                                        ExitStockTradesByForce(runningTick, Trade.TypeOfTrade.MIS, "Max Stock Profit reached for the day")
                                                        stockList(stockName).EligibleToTakeTrade = False
                                                    ElseIf StockPLAfterBrokerage(tradeCheckingDate, runningTick.TradingSymbol) <= Math.Abs(stockStrategyRule.MaxLossOfThisStock) * -1 Then
                                                        ExitStockTradesByForce(runningTick, Trade.TypeOfTrade.MIS, "Max Stock Loss reached for the day")
                                                        stockList(stockName).EligibleToTakeTrade = False
                                                    End If

                                                    'Force exit at day end
                                                    _canceller.Token.ThrowIfCancellationRequested()
                                                    Dim eodTime As Date = New Date(runningTick.PayloadDate.Year, runningTick.PayloadDate.Month, runningTick.PayloadDate.Day, Me.EODExitTime.Hours, Me.EODExitTime.Minutes, Me.EODExitTime.Seconds)
                                                    If runningTick.PayloadDate >= eodTime Then
                                                        ExitStockTradesByForce(runningTick, Trade.TypeOfTrade.MIS, "EOD Exit")
                                                        stockList(stockName).EligibleToTakeTrade = False
                                                    End If

                                                    'Stock MTM Check
                                                    _canceller.Token.ThrowIfCancellationRequested()
                                                    If ExitOnStockFixedTargetStoploss Then
                                                        If StockPLAfterBrokerage(tradeCheckingDate, runningTick.TradingSymbol) >= StockMaxProfitPerDay Then
                                                            ExitStockTradesByForce(runningTick, Trade.TypeOfTrade.MIS, "Max Stock Profit reached for the day")
                                                            stockList(stockName).EligibleToTakeTrade = False
                                                        ElseIf StockPLAfterBrokerage(tradeCheckingDate, runningTick.TradingSymbol) <= Math.Abs(StockMaxLossPerDay) * -1 Then
                                                            ExitStockTradesByForce(runningTick, Trade.TypeOfTrade.MIS, "Max Stock Loss reached for the day")
                                                            stockList(stockName).EligibleToTakeTrade = False
                                                        End If
                                                    End If

                                                    'OverAll MTM Check
                                                    _canceller.Token.ThrowIfCancellationRequested()
                                                    If ExitOnOverAllFixedTargetStoploss Then
                                                        If TotalPLAfterBrokerage(tradeCheckingDate) >= OverAllProfitPerDay Then
                                                            ExitAllTradeByForce(potentialTickSignalTime, currentDayOneMinuteStocksPayload, Trade.TypeOfTrade.MIS, "Max Profit reached for the day")
                                                            stockList(stockName).EligibleToTakeTrade = False
                                                        ElseIf TotalPLAfterBrokerage(tradeCheckingDate) <= Math.Abs(OverAllLossPerDay) * -1 Then
                                                            ExitAllTradeByForce(potentialTickSignalTime, currentDayOneMinuteStocksPayload, Trade.TypeOfTrade.MIS, "Max Loss reached for the day")
                                                            stockList(stockName).EligibleToTakeTrade = False
                                                        ElseIf Me.TypeOfMTMTrailing = MTMTrailingType.FixedSlabTrailing AndAlso TotalPLAfterBrokerage(tradeCheckingDate) <= OverAllLossPerDay Then
                                                            ExitAllTradeByForce(potentialTickSignalTime, currentDayOneMinuteStocksPayload, Trade.TypeOfTrade.MIS, "Trailing MTM reached for the day")
                                                            stockList(stockName).EligibleToTakeTrade = False
                                                        ElseIf Me.TypeOfMTMTrailing = MTMTrailingType.LogSlabTrailing AndAlso TotalPLAfterBrokerage(tradeCheckingDate) <= OverAllLossPerDay Then
                                                            ExitAllTradeByForce(potentialTickSignalTime, currentDayOneMinuteStocksPayload, Trade.TypeOfTrade.MIS, "Log MTM reached for the day")
                                                            stockList(stockName).EligibleToTakeTrade = False
                                                        ElseIf Me.TypeOfMTMTrailing = MTMTrailingType.RealtimeTrailing AndAlso TotalPLAfterBrokerage(tradeCheckingDate) <= OverAllLossPerDay Then
                                                            ExitAllTradeByForce(potentialTickSignalTime, currentDayOneMinuteStocksPayload, Trade.TypeOfTrade.MIS, "Realtime Trailing MTM reached for the day")
                                                            stockList(stockName).EligibleToTakeTrade = False
                                                        End If
                                                    End If

                                                    'Exit Trade From Rule
                                                    _canceller.Token.ThrowIfCancellationRequested()
                                                    Dim potentialRuleCancelTrades As List(Of Trade) = GetSpecificTrades(currentMinuteCandlePayload, Trade.TypeOfTrade.MIS, Trade.TradeExecutionStatus.Open)
                                                    If potentialRuleCancelTrades IsNot Nothing AndAlso potentialRuleCancelTrades.Count > 0 Then
                                                        If Me.TickBasedStrategy OrElse Not stockList(stockName).CancelOrderDoneForTheMinute Then
                                                            For Each runningCancelTrade In potentialRuleCancelTrades
                                                                _canceller.Token.ThrowIfCancellationRequested()
                                                                Dim exitOrderDetails As Tuple(Of Boolean, String) = Await stockStrategyRule.IsTriggerReceivedForExitOrderAsync(runningTick, runningCancelTrade).ConfigureAwait(False)
                                                                If exitOrderDetails IsNot Nothing AndAlso exitOrderDetails.Item1 Then
                                                                    _canceller.Token.ThrowIfCancellationRequested()
                                                                    ExitTradeByForce(runningCancelTrade, runningTick, exitOrderDetails.Item2)
                                                                End If
                                                            Next
                                                            stockList(stockName).CancelOrderDoneForTheMinute = True
                                                        End If
                                                    End If
                                                    _canceller.Token.ThrowIfCancellationRequested()
                                                    Dim potentialRuleExitTrades As List(Of Trade) = GetSpecificTrades(currentMinuteCandlePayload, Trade.TypeOfTrade.MIS, Trade.TradeExecutionStatus.Inprogress)
                                                    If potentialRuleExitTrades IsNot Nothing AndAlso potentialRuleExitTrades.Count > 0 Then
                                                        If Me.TickBasedStrategy OrElse Not stockList(stockName).ExitOrderDoneForTheMinute Then
                                                            For Each runningExitTrade In potentialRuleExitTrades
                                                                _canceller.Token.ThrowIfCancellationRequested()
                                                                Dim exitOrderDetails As Tuple(Of Boolean, String) = Await stockStrategyRule.IsTriggerReceivedForExitOrderAsync(runningTick, runningExitTrade).ConfigureAwait(False)
                                                                If exitOrderDetails IsNot Nothing AndAlso exitOrderDetails.Item1 Then
                                                                    ExitTradeByForce(runningExitTrade, runningTick, exitOrderDetails.Item2)
                                                                End If
                                                            Next
                                                            stockList(stockName).ExitOrderDoneForTheMinute = True
                                                        End If
                                                    End If

                                                    'Place Order
                                                    _canceller.Token.ThrowIfCancellationRequested()
                                                    Dim placeOrderDetails As Tuple(Of Boolean, List(Of PlaceOrderParameters)) = Nothing
                                                    If stockList(stockName).EligibleToTakeTrade Then
                                                        If Me.TickBasedStrategy OrElse Not stockList(stockName).PlaceOrderDoneForTheMinute Then
                                                            If Me.StockNumberOfTrades(runningTick.PayloadDate, runningTick.TradingSymbol) < Me.NumberOfTradesPerStockPerDay AndAlso
                                                                Me.TotalPLAfterBrokerage(runningTick.PayloadDate) < Me.OverAllProfitPerDay AndAlso
                                                                Me.TotalPLAfterBrokerage(runningTick.PayloadDate) > Me.OverAllLossPerDay AndAlso
                                                                Me.StockPLAfterBrokerage(runningTick.PayloadDate, runningTick.TradingSymbol) < Me.StockMaxProfitPerDay AndAlso
                                                                Me.StockPLAfterBrokerage(runningTick.PayloadDate, runningTick.TradingSymbol) > Math.Abs(Me.StockMaxLossPerDay) * -1 AndAlso
                                                                Me.StockPLAfterBrokerage(runningTick.PayloadDate, runningTick.TradingSymbol) < stockStrategyRule.MaxProfitOfThisStock AndAlso
                                                                Me.StockPLAfterBrokerage(runningTick.PayloadDate, runningTick.TradingSymbol) > Math.Abs(stockStrategyRule.MaxLossOfThisStock) * -1 Then
                                                                placeOrderDetails = Await stockStrategyRule.IsTriggerReceivedForPlaceOrderAsync(runningTick).ConfigureAwait(False)
                                                                stockList(stockName).PlaceOrderTrigger = placeOrderDetails
                                                                stockList(stockName).PlaceOrderDoneForTheMinute = True
                                                            End If
                                                        Else
                                                            If Me.StockNumberOfTrades(runningTick.PayloadDate, runningTick.TradingSymbol) < Me.NumberOfTradesPerStockPerDay AndAlso
                                                                Me.TotalPLAfterBrokerage(runningTick.PayloadDate) < Me.OverAllProfitPerDay AndAlso
                                                                Me.TotalPLAfterBrokerage(runningTick.PayloadDate) > Me.OverAllLossPerDay AndAlso
                                                                Me.StockPLAfterBrokerage(runningTick.PayloadDate, runningTick.TradingSymbol) < Me.StockMaxProfitPerDay AndAlso
                                                                Me.StockPLAfterBrokerage(runningTick.PayloadDate, runningTick.TradingSymbol) > Math.Abs(Me.StockMaxLossPerDay) * -1 AndAlso
                                                                Me.StockPLAfterBrokerage(runningTick.PayloadDate, runningTick.TradingSymbol) < stockStrategyRule.MaxProfitOfThisStock AndAlso
                                                                Me.StockPLAfterBrokerage(runningTick.PayloadDate, runningTick.TradingSymbol) > Math.Abs(stockStrategyRule.MaxLossOfThisStock) * -1 Then
                                                                placeOrderDetails = stockList(stockName).PlaceOrderTrigger
                                                            End If
                                                        End If
                                                    End If
                                                    If placeOrderDetails IsNot Nothing AndAlso placeOrderDetails.Item1 Then
                                                        Dim placeOrders As List(Of PlaceOrderParameters) = placeOrderDetails.Item2
                                                        If placeOrders IsNot Nothing AndAlso placeOrders.Count > 0 Then
                                                            Dim tradeTag As String = System.Guid.NewGuid.ToString()
                                                            For Each runningOrder In placeOrders
                                                                _canceller.Token.ThrowIfCancellationRequested()
                                                                If runningOrder.Used Then
                                                                    Continue For
                                                                End If
                                                                Select Case runningOrder.OrderType
                                                                    Case Trade.TypeOfOrder.SL
                                                                        If runningOrder.EntryDirection = Trade.TradeExecutionDirection.Buy Then
                                                                            If runningTick.High >= runningOrder.EntryPrice Then
                                                                                Continue For
                                                                            End If
                                                                        ElseIf runningOrder.EntryDirection = Trade.TradeExecutionDirection.Sell Then
                                                                            If runningTick.Low <= runningOrder.EntryPrice Then
                                                                                Continue For
                                                                            End If
                                                                        End If
                                                                    Case Trade.TypeOfOrder.Market
                                                                        runningOrder.EntryPrice = runningTick.Open
                                                                    Case Else
                                                                        Throw New NotImplementedException
                                                                End Select
                                                                Dim runningTrade As Trade = New Trade(originatingStrategy:=Me,
                                                                                                      tradingSymbol:=runningTick.TradingSymbol,
                                                                                                      stockType:=Me.StockType,
                                                                                                      orderType:=runningOrder.OrderType,
                                                                                                      tradingDate:=runningTick.PayloadDate,
                                                                                                      entryDirection:=runningOrder.EntryDirection,
                                                                                                      entryPrice:=runningOrder.EntryPrice,
                                                                                                      entryBuffer:=runningOrder.Buffer,
                                                                                                      squareOffType:=Trade.TypeOfTrade.MIS,
                                                                                                      entryCondition:=Trade.TradeEntryCondition.Original,
                                                                                                      entryRemark:="Original Entry",
                                                                                                      quantity:=runningOrder.Quantity,
                                                                                                      lotSize:=stockStrategyRule.LotSize,
                                                                                                      potentialTarget:=runningOrder.Target,
                                                                                                      targetRemark:=Math.Abs(runningOrder.EntryPrice - runningOrder.Target),
                                                                                                      potentialStopLoss:=runningOrder.Stoploss,
                                                                                                      stoplossBuffer:=runningOrder.Buffer,
                                                                                                      slRemark:=Math.Abs(runningOrder.EntryPrice - runningOrder.Stoploss),
                                                                                                      signalCandle:=runningOrder.SignalCandle)

                                                                runningTrade.UpdateTrade(Tag:=tradeTag,
                                                                                         SquareOffValue:=Math.Abs(runningOrder.EntryPrice - runningOrder.Target),
                                                                                         Supporting1:=runningOrder.Supporting1,
                                                                                         Supporting2:=runningOrder.Supporting2,
                                                                                         Supporting3:=runningOrder.Supporting3,
                                                                                         Supporting4:=runningOrder.Supporting4,
                                                                                         Supporting5:=runningOrder.Supporting5)

                                                                If PlaceOrModifyOrder(runningTrade, Nothing) Then
                                                                    runningOrder.Used = True
                                                                End If
                                                            Next
                                                        End If
                                                    End If

                                                    'Exit Trade
                                                    _canceller.Token.ThrowIfCancellationRequested()
                                                    Dim exitOrderSuccessful As Boolean = False
                                                    Dim potentialExitTrades As List(Of Trade) = GetSpecificTrades(currentMinuteCandlePayload, Trade.TypeOfTrade.MIS, Trade.TradeExecutionStatus.Inprogress)
                                                    If potentialExitTrades IsNot Nothing AndAlso potentialExitTrades.Count > 0 Then
                                                        For Each runningPotentialExitTrade In potentialExitTrades
                                                            _canceller.Token.ThrowIfCancellationRequested()
                                                            exitOrderSuccessful = ExitTradeIfPossible(runningPotentialExitTrade, runningTick)
                                                        Next
                                                    End If

                                                    'Place Order
                                                    _canceller.Token.ThrowIfCancellationRequested()
                                                    Dim placeOrderTrigger As Tuple(Of Boolean, List(Of PlaceOrderParameters)) = Nothing
                                                    If exitOrderSuccessful Then
                                                        If Me.StockNumberOfTrades(runningTick.PayloadDate, runningTick.TradingSymbol) < Me.NumberOfTradesPerStockPerDay AndAlso
                                                            Me.TotalPLAfterBrokerage(runningTick.PayloadDate) < Me.OverAllProfitPerDay AndAlso
                                                            Me.TotalPLAfterBrokerage(runningTick.PayloadDate) > Me.OverAllLossPerDay AndAlso
                                                            Me.StockPLAfterBrokerage(runningTick.PayloadDate, runningTick.TradingSymbol) < Me.StockMaxProfitPerDay AndAlso
                                                            Me.StockPLAfterBrokerage(runningTick.PayloadDate, runningTick.TradingSymbol) > Math.Abs(Me.StockMaxLossPerDay) * -1 AndAlso
                                                            Me.StockPLAfterBrokerage(runningTick.PayloadDate, runningTick.TradingSymbol) < stockStrategyRule.MaxProfitOfThisStock AndAlso
                                                            Me.StockPLAfterBrokerage(runningTick.PayloadDate, runningTick.TradingSymbol) > Math.Abs(stockStrategyRule.MaxLossOfThisStock) * -1 Then
                                                            placeOrderTrigger = Await stockStrategyRule.IsTriggerReceivedForPlaceOrderAsync(runningTick).ConfigureAwait(False)
                                                            stockList(stockName).PlaceOrderTrigger = placeOrderTrigger
                                                        End If
                                                    End If
                                                    If placeOrderTrigger IsNot Nothing AndAlso placeOrderTrigger.Item1 Then
                                                        Dim placeOrders As List(Of PlaceOrderParameters) = placeOrderTrigger.Item2
                                                        If placeOrders IsNot Nothing AndAlso placeOrders.Count > 0 Then
                                                            Dim tradeTag As String = System.Guid.NewGuid.ToString()
                                                            For Each runningOrder In placeOrders
                                                                _canceller.Token.ThrowIfCancellationRequested()
                                                                If runningOrder.Used Then
                                                                    Continue For
                                                                End If
                                                                Select Case runningOrder.OrderType
                                                                    Case Trade.TypeOfOrder.SL
                                                                        If runningOrder.EntryDirection = Trade.TradeExecutionDirection.Buy Then
                                                                            If runningTick.High >= runningOrder.EntryPrice Then
                                                                                Continue For
                                                                            End If
                                                                        ElseIf runningOrder.EntryDirection = Trade.TradeExecutionDirection.Sell Then
                                                                            If runningTick.Low <= runningOrder.EntryPrice Then
                                                                                Continue For
                                                                            End If
                                                                        End If
                                                                    Case Trade.TypeOfOrder.Market
                                                                        runningOrder.EntryPrice = runningTick.Open
                                                                    Case Else
                                                                        Throw New NotImplementedException
                                                                End Select
                                                                Dim runningTrade As Trade = New Trade(originatingStrategy:=Me,
                                                                                                      tradingSymbol:=runningTick.TradingSymbol,
                                                                                                      stockType:=Me.StockType,
                                                                                                      orderType:=runningOrder.OrderType,
                                                                                                      tradingDate:=runningTick.PayloadDate,
                                                                                                      entryDirection:=runningOrder.EntryDirection,
                                                                                                      entryPrice:=runningOrder.EntryPrice,
                                                                                                      entryBuffer:=runningOrder.Buffer,
                                                                                                      squareOffType:=Trade.TypeOfTrade.MIS,
                                                                                                      entryCondition:=Trade.TradeEntryCondition.Original,
                                                                                                      entryRemark:="Original Entry",
                                                                                                      quantity:=runningOrder.Quantity,
                                                                                                      lotSize:=stockStrategyRule.LotSize,
                                                                                                      potentialTarget:=runningOrder.Target,
                                                                                                      targetRemark:=Math.Abs(runningOrder.EntryPrice - runningOrder.Target),
                                                                                                      potentialStopLoss:=runningOrder.Stoploss,
                                                                                                      stoplossBuffer:=runningOrder.Buffer,
                                                                                                      slRemark:=Math.Abs(runningOrder.EntryPrice - runningOrder.Stoploss),
                                                                                                      signalCandle:=runningOrder.SignalCandle)

                                                                runningTrade.UpdateTrade(Tag:=tradeTag,
                                                                                         SquareOffValue:=Math.Abs(runningOrder.EntryPrice - runningOrder.Target),
                                                                                         Supporting1:=runningOrder.Supporting1,
                                                                                         Supporting2:=runningOrder.Supporting2,
                                                                                         Supporting3:=runningOrder.Supporting3,
                                                                                         Supporting4:=runningOrder.Supporting4,
                                                                                         Supporting5:=runningOrder.Supporting5)

                                                                If PlaceOrModifyOrder(runningTrade, Nothing) Then
                                                                    runningOrder.Used = True
                                                                End If
                                                            Next
                                                        End If
                                                    End If

                                                    'Enter Trade
                                                    _canceller.Token.ThrowIfCancellationRequested()
                                                    Dim potentialEntryTrades As List(Of Trade) = GetSpecificTrades(currentMinuteCandlePayload, Trade.TypeOfTrade.MIS, Trade.TradeExecutionStatus.Open)
                                                    If potentialEntryTrades IsNot Nothing AndAlso potentialEntryTrades.Count > 0 Then
                                                        For Each runningPotentialEntryTrade In potentialEntryTrades
                                                            _canceller.Token.ThrowIfCancellationRequested()
                                                            EnterTradeIfPossible(runningPotentialEntryTrade, runningTick)
                                                        Next
                                                    End If

                                                    'Modify Stoploss Trade
                                                    _canceller.Token.ThrowIfCancellationRequested()
                                                    Dim potentialModifySLTrades As List(Of Trade) = GetSpecificTrades(currentMinuteCandlePayload, Trade.TypeOfTrade.MIS, Trade.TradeExecutionStatus.Inprogress)
                                                    If potentialModifySLTrades IsNot Nothing AndAlso potentialModifySLTrades.Count > 0 Then
                                                        If Me.TickBasedStrategy OrElse Not stockList(stockName).ModifyStoplossOrderDoneForTheMinute OrElse Me.TrailingStoploss Then
                                                            For Each runningModifyTrade In potentialModifySLTrades
                                                                _canceller.Token.ThrowIfCancellationRequested()
                                                                Dim modifyOrderDetails As Tuple(Of Boolean, Decimal, String) = Await stockStrategyRule.IsTriggerReceivedForModifyStoplossOrderAsync(runningTick, runningModifyTrade).ConfigureAwait(False)
                                                                If modifyOrderDetails IsNot Nothing AndAlso modifyOrderDetails.Item1 Then
                                                                    _canceller.Token.ThrowIfCancellationRequested()
                                                                    runningModifyTrade.UpdateTrade(PotentialStopLoss:=modifyOrderDetails.Item2, SLRemark:=modifyOrderDetails.Item3)
                                                                End If
                                                            Next
                                                            stockList(stockName).ModifyStoplossOrderDoneForTheMinute = True
                                                        End If
                                                    End If

                                                    'Modify Target Trade
                                                    _canceller.Token.ThrowIfCancellationRequested()
                                                    Dim potentialModifyTargetTrades As List(Of Trade) = GetSpecificTrades(currentMinuteCandlePayload, Trade.TypeOfTrade.MIS, Trade.TradeExecutionStatus.Inprogress)
                                                    If potentialModifyTargetTrades IsNot Nothing AndAlso potentialModifyTargetTrades.Count > 0 Then
                                                        If Me.TickBasedStrategy OrElse Not stockList(stockName).ModifyTargetOrderDoneForTheMinute Then
                                                            For Each runningModifyTrade In potentialModifyTargetTrades
                                                                _canceller.Token.ThrowIfCancellationRequested()
                                                                Dim modifyOrderDetails As Tuple(Of Boolean, Decimal, String) = Await stockStrategyRule.IsTriggerReceivedForModifyTargetOrderAsync(runningTick, runningModifyTrade).ConfigureAwait(False)
                                                                If modifyOrderDetails IsNot Nothing AndAlso modifyOrderDetails.Item1 Then
                                                                    _canceller.Token.ThrowIfCancellationRequested()
                                                                    runningModifyTrade.UpdateTrade(PotentialTarget:=modifyOrderDetails.Item2, TargetRemark:=modifyOrderDetails.Item3)
                                                                End If
                                                            Next
                                                            stockList(stockName).ModifyTargetOrderDoneForTheMinute = True
                                                        End If
                                                    End If
                                                Next
                                            End If
                                        End If
                                    Next
                                    startSecond = startSecond.Add(TimeSpan.FromSeconds(1))
                                End While   'Second Loop
                                startMinute = startMinute.Add(TimeSpan.FromMinutes(Me.SignalTimeFrame))
                            End While   'Minute Loop
                            ExitAllTradeByForce(tradeCheckingDate, currentDayOneMinuteStocksPayload, Trade.TypeOfTrade.MIS, "Special Force Close")
                        End If
                    End If
                    SetOverallDrawUpDrawDownForTheDay(tradeCheckingDate)
                    totalPL += TotalPLAfterBrokerage(tradeCheckingDate)
                    tradeCheckingDate = tradeCheckingDate.AddDays(1)

                    'Serialization
                    If TradesTaken IsNot Nothing AndAlso TradesTaken.Count > 0 Then
                        OnHeartbeat("Serializing Trades collection")
                        SerializeFromCollectionUsingFileStream(Of Dictionary(Of Date, Dictionary(Of String, List(Of Trade))))(tradesFileName, TradesTaken)
                    End If
                End While   'Date Loop

                If CapitalMovement IsNot Nothing Then Utilities.Strings.SerializeFromCollection(Of Dictionary(Of Date, List(Of Capital)))(capitalFileName, CapitalMovement)

                PrintArrayToExcel(filename, tradesFileName, capitalFileName)
            End If
        End Function


#Region "Stock Selection"
        Private Async Function GetStockData(tradingDate As Date) As Task(Of Dictionary(Of String, StockDetails))
            Dim ret As Dictionary(Of String, StockDetails) = Nothing
            If Me.StockFileName IsNot Nothing Then
                Dim dt As DataTable = Nothing
                Using csvHelper As New Utilities.DAL.CSVHelper(Me.StockFileName, ",", _canceller)
                    dt = csvHelper.GetDataTableFromCSV(1)
                End Using
                If dt IsNot Nothing AndAlso dt.Rows.Count > 0 Then
                    Select Case Me.RuleNumber
                        Case 13
                            Dim counter As Integer = 0
                            Dim stockList As Dictionary(Of String, StockDetails) = Nothing
                            For i = 1 To dt.Rows.Count - 1
                                Dim rowDate As Date = dt.Rows(i)(0)
                                If rowDate.Date = tradingDate.Date Then
                                    If stockList Is Nothing Then stockList = New Dictionary(Of String, StockDetails)
                                    Dim tradingSymbol As String = dt.Rows(i).Item(1)
                                    Dim instrumentName As String = Nothing
                                    If tradingSymbol.Contains("FUT") Then
                                        instrumentName = tradingSymbol.Remove(tradingSymbol.Count - 8)
                                    Else
                                        instrumentName = tradingSymbol
                                    End If
                                    Dim stockPayload As Dictionary(Of Date, Payload) = Nothing
                                    If Me.DataSource = SourceOfData.Database Then
                                        stockPayload = Cmn.GetRawPayload(Me.DatabaseTable, instrumentName, tradingDate, tradingDate)
                                    ElseIf Me.DataSource = SourceOfData.Live Then
                                        stockPayload = Await Cmn.GetHistoricalDataAsync(Me.DatabaseTable, instrumentName, tradingDate, tradingDate).ConfigureAwait(False)
                                    End If
                                    If stockPayload IsNot Nothing AndAlso stockPayload.Count > 0 Then
                                        Dim time As Date = New Date(tradingDate.Year, tradingDate.Month, tradingDate.Day, 9, 16, 0)
                                        Dim stockPrice As Decimal = stockPayload.Where(Function(x)
                                                                                           Return x.Key >= time
                                                                                       End Function).FirstOrDefault.Value.Close
                                        Dim quantity As Integer = CalculateQuantityFromInvestment(dt.Rows(i).Item(2), CType(Me.RuleEntityData, LowStoplossStrategyRule.StrategyRuleEntities).MinimumCapital, stockPrice, Me.StockType, True)
                                        Dim capital As Decimal = stockPrice * quantity / Me.MarginMultiplier
                                        If capital < 25000 Then
                                            Dim stoploss As Decimal = CalculatorTargetOrStoploss(instrumentName, stockPrice, quantity, Math.Abs(CType(Me.RuleEntityData, LowStoplossStrategyRule.StrategyRuleEntities).MaxStoploss) * -1, Trade.TradeExecutionDirection.Buy, Me.StockType)
                                            Dim slPoint As Decimal = stockPrice - stoploss - Me.TickSize
                                            If slPoint > 0.2 Then
                                                Dim detailsOfStock As StockDetails = New StockDetails With
                                                {.StockName = instrumentName,
                                                .LotSize = dt.Rows(i).Item(2),
                                                .EligibleToTakeTrade = True,
                                                .Supporting1 = dt.Rows(i).Item(3),
                                                .Supporting2 = dt.Rows(i).Item(5),
                                                .Supporting3 = slPoint,
                                                .Supporting4 = quantity}
                                                stockList.Add(instrumentName, detailsOfStock)
                                                counter += 1
                                                If CType(Me.RuleEntityData, LowStoplossStrategyRule.StrategyRuleEntities).TypeOfSignal = LowStoplossStrategyRule.SignalType.PreviousDayHighLow Then
                                                    Dim openPrice As Decimal = stockPayload.Where(Function(x)
                                                                                                      Return x.Key >= New Date(tradingDate.Year, tradingDate.Month, tradingDate.Day, 9, 15, 0)
                                                                                                  End Function).FirstOrDefault.Value.Open
                                                    Dim previousDayLow As Decimal = dt.Rows(i).Item(7)
                                                    Dim previousDayHigh As Decimal = dt.Rows(i).Item(8)
                                                    Dim previousDayClose As Decimal = dt.Rows(i).Item(9)
                                                    If openPrice > previousDayHigh Then
                                                        detailsOfStock.Supporting5 = previousDayHigh - openPrice
                                                        detailsOfStock.Supporting6 = 0
                                                    ElseIf openPrice < previousDayLow Then
                                                        detailsOfStock.Supporting5 = openPrice - previousDayLow
                                                        detailsOfStock.Supporting6 = 0
                                                    Else
                                                        detailsOfStock.Supporting5 = 0
                                                        detailsOfStock.Supporting6 = Math.Abs(previousDayClose - openPrice)
                                                    End If
                                                Else
                                                    If counter = Me.NumberOfTradeableStockPerDay Then Exit For
                                                End If
                                            End If
                                        End If
                                    End If
                                End If
                            Next
                            If CType(Me.RuleEntityData, LowStoplossStrategyRule.StrategyRuleEntities).TypeOfSignal = LowStoplossStrategyRule.SignalType.PreviousDayHighLow Then
                                If stockList IsNot Nothing AndAlso stockList.Count > 0 Then
                                    Dim gapupStocks As IEnumerable(Of KeyValuePair(Of String, StockDetails)) = stockList.Where(Function(x)
                                                                                                                                   Return x.Value.Supporting5 > 0
                                                                                                                               End Function)
                                    Dim gapdownStocks As IEnumerable(Of KeyValuePair(Of String, StockDetails)) = stockList.Where(Function(x)
                                                                                                                                     Return x.Value.Supporting5 < 0
                                                                                                                                 End Function)
                                    Dim nmbrOfGapupStock As Integer = Math.Ceiling(Me.NumberOfTradeableStockPerDay / 2)
                                    Dim nmbrOfGapdownStock As Integer = nmbrOfGapupStock
                                    Dim totalNmbr As Integer = nmbrOfGapupStock + nmbrOfGapdownStock
                                    If gapupStocks Is Nothing OrElse gapupStocks.Count < nmbrOfGapupStock Then
                                        nmbrOfGapdownStock = totalNmbr - gapupStocks.Count
                                    End If
                                    If gapdownStocks Is Nothing OrElse gapdownStocks.Count < nmbrOfGapdownStock Then
                                        nmbrOfGapupStock = totalNmbr - gapdownStocks.Count
                                    End If
                                    If gapupStocks IsNot Nothing AndAlso gapupStocks.Count > 0 Then
                                        Dim ctr As Integer = 0
                                        For Each runningStock In gapupStocks.OrderByDescending(Function(x)
                                                                                                   Return x.Value.Supporting1
                                                                                               End Function)
                                            If ret Is Nothing Then ret = New Dictionary(Of String, StockDetails)
                                            ret.Add(runningStock.Key, runningStock.Value)
                                            ctr += 1
                                            If ctr = nmbrOfGapupStock Then Exit For
                                        Next
                                    End If
                                    If gapdownStocks IsNot Nothing AndAlso gapdownStocks.Count > 0 Then
                                        Dim ctr As Integer = 0
                                        For Each runningStock In gapdownStocks.OrderByDescending(Function(x)
                                                                                                     Return x.Value.Supporting1
                                                                                                 End Function)
                                            If ret Is Nothing Then ret = New Dictionary(Of String, StockDetails)
                                            ret.Add(runningStock.Key, runningStock.Value)
                                            ctr += 1
                                            If ctr = nmbrOfGapdownStock Then Exit For
                                        Next
                                    End If
                                    If ret Is Nothing OrElse ret.Count < totalNmbr Then
                                        Dim nmbrOfStockNeedToAdd As Integer = totalNmbr - ret.Count
                                        Dim ctr As Integer = 0
                                        For Each runningStock In stockList.OrderByDescending(Function(x)
                                                                                                 Return x.Value.Supporting6
                                                                                             End Function)
                                            If ret Is Nothing Then ret = New Dictionary(Of String, StockDetails)
                                            ret.Add(runningStock.Key, runningStock.Value)
                                            ctr += 1
                                            If ctr = nmbrOfStockNeedToAdd Then Exit For
                                        Next
                                    End If
                                End If
                            Else
                                ret = stockList
                            End If
                        Case 14
                            For i = 1 To dt.Rows.Count - 1
                                If ret Is Nothing Then ret = New Dictionary(Of String, StockDetails)
                                Dim instrumentName As String = dt.Rows(i)(0)
                                Dim tradingSymbol As String = Cmn.GetCurrentTradingSymbol(Common.DataBaseTable.EOD_Futures, tradingDate, instrumentName)
                                If tradingSymbol IsNot Nothing Then
                                    Dim lotSize As Integer = Cmn.GetLotSize(Common.DataBaseTable.EOD_Futures, tradingSymbol, tradingDate)
                                    If lotSize <> Integer.MinValue Then
                                        Dim detailsOfStock As StockDetails = New StockDetails With
                                                {.StockName = instrumentName,
                                                .LotSize = lotSize,
                                                .EligibleToTakeTrade = True}
                                        ret.Add(instrumentName, detailsOfStock)
                                    End If
                                End If
                            Next
                        Case 17
                            Dim counter As Integer = 0
                            For i = 1 To dt.Rows.Count - 1
                                Dim rowDate As Date = dt.Rows(i)(0)
                                If rowDate.Date = tradingDate.Date Then
                                    If ret Is Nothing Then ret = New Dictionary(Of String, StockDetails)
                                    Dim tradingSymbol As String = dt.Rows(i).Item(1)
                                    Dim instrumentName As String = Nothing
                                    If tradingSymbol.Contains("FUT") Then
                                        instrumentName = tradingSymbol.Remove(tradingSymbol.Count - 8)
                                    Else
                                        instrumentName = tradingSymbol
                                    End If
                                    Dim stockPayload As Dictionary(Of Date, Payload) = Nothing
                                    If Me.DataSource = SourceOfData.Database Then
                                        stockPayload = Cmn.GetRawPayload(Me.DatabaseTable, instrumentName, tradingDate, tradingDate)
                                    ElseIf Me.DataSource = SourceOfData.Live Then
                                        stockPayload = Await Cmn.GetHistoricalDataAsync(Me.DatabaseTable, instrumentName, tradingDate, tradingDate).ConfigureAwait(False)
                                    End If
                                    If stockPayload IsNot Nothing AndAlso stockPayload.Count > 0 Then
                                        Dim time As Date = New Date(tradingDate.Year, tradingDate.Month, tradingDate.Day, 9, 16, 0)
                                        Dim stockPrice As Decimal = stockPayload.Where(Function(x)
                                                                                           Return x.Key >= time
                                                                                       End Function).FirstOrDefault.Value.Close
                                        Dim quantity As Integer = CalculateQuantityFromInvestment(dt.Rows(i).Item(2), CType(Me.RuleEntityData, LowSLPinbarStrategyRule.StrategyRuleEntities).MinimumInvestmentPerStock, stockPrice, Me.StockType, True)
                                        Dim capital As Decimal = stockPrice * quantity / Me.MarginMultiplier
                                        If capital < 25000 Then
                                            Dim stoploss As Decimal = CalculatorTargetOrStoploss(instrumentName, stockPrice, quantity, Math.Abs(CType(Me.RuleEntityData, LowSLPinbarStrategyRule.StrategyRuleEntities).MaxLossPerTrade) * -1, Trade.TradeExecutionDirection.Buy, Me.StockType)
                                            Dim slPoint As Decimal = stockPrice - stoploss
                                            Dim detailsOfStock As StockDetails = New StockDetails With
                                            {.StockName = instrumentName,
                                            .LotSize = dt.Rows(i).Item(2),
                                            .EligibleToTakeTrade = True,
                                            .Supporting1 = dt.Rows(i).Item(3),
                                            .Supporting2 = dt.Rows(i).Item(5),
                                            .Supporting3 = slPoint,
                                            .Supporting4 = quantity}
                                            ret.Add(instrumentName, detailsOfStock)
                                            counter += 1
                                            If counter = Me.NumberOfTradeableStockPerDay Then Exit For
                                        End If
                                    End If
                                End If
                            Next
                        Case 32
                            Dim tradingDay As Boolean = Await Cmn.IsTradingDay(tradingDate).ConfigureAwait(False)
                            If tradingDay Then
                                Dim previousTradingDay As Date = Cmn.GetPreviousTradingDay(Common.DataBaseTable.EOD_Cash, tradingDate)
                                Dim stockList As Dictionary(Of String, Decimal()) = Nothing
                                For i = 1 To dt.Rows.Count - 1
                                    Dim rowDate As Date = dt.Rows(i)(0)
                                    If rowDate.Date = previousTradingDay.Date Then
                                        If stockList Is Nothing Then stockList = New Dictionary(Of String, Decimal())
                                        Dim tradingSymbol As String = dt.Rows(i).Item(1)
                                        Dim instrumentName As String = Nothing
                                        If tradingSymbol.Contains("FUT") Then
                                            instrumentName = tradingSymbol.Remove(tradingSymbol.Count - 8)
                                        Else
                                            instrumentName = tradingSymbol
                                        End If
                                        stockList.Add(instrumentName, {dt.Rows(i).Item(11), dt.Rows(i).Item(5)})
                                    End If
                                Next
                                If stockList IsNot Nothing AndAlso stockList.Count > 0 Then
                                    Dim minumumGainLossPercentage As Decimal = 0.5
                                    Dim counter As Integer = 0
                                    For Each runningStock In stockList.OrderBy(Function(x)
                                                                                   Return x.Value(0)
                                                                               End Function)
                                        If runningStock.Value(0) < minumumGainLossPercentage * -1 Then
                                            Dim detailsOfStock As StockDetails = New StockDetails With
                                                    {.StockName = runningStock.Key,
                                                    .LotSize = 1,
                                                    .EligibleToTakeTrade = True,
                                                    .Supporting1 = -1,
                                                    .Supporting2 = runningStock.Value(1)}
                                            If ret Is Nothing Then ret = New Dictionary(Of String, StockDetails)
                                            ret.Add(runningStock.Key, detailsOfStock)
                                            counter += 1
                                            If counter = Me.NumberOfTradeableStockPerDay Then Exit For
                                        End If
                                    Next
                                    counter = 0
                                    For Each runningStock In stockList.OrderByDescending(Function(x)
                                                                                             Return x.Value(0)
                                                                                         End Function)
                                        If runningStock.Value(0) > minumumGainLossPercentage Then
                                            Dim detailsOfStock As StockDetails = New StockDetails With
                                                    {.StockName = runningStock.Key,
                                                    .LotSize = 1,
                                                    .EligibleToTakeTrade = True,
                                                    .Supporting1 = 1,
                                                    .Supporting2 = runningStock.Value(1)}
                                            If ret Is Nothing Then ret = New Dictionary(Of String, StockDetails)
                                            ret.Add(runningStock.Key, detailsOfStock)
                                            counter += 1
                                            If counter = Me.NumberOfTradeableStockPerDay Then Exit For
                                        End If
                                    Next
                                End If
                            End If
                        Case 36
                            For i = 1 To dt.Rows.Count - 1
                                If ret Is Nothing Then ret = New Dictionary(Of String, StockDetails)
                                Dim tradingSymbol As String = dt.Rows(i).Item(0)
                                Dim direction As String = dt.Rows(i).Item(1)
                                Dim instrumentName As String = Nothing
                                If tradingSymbol.Contains("FUT") Then
                                    instrumentName = tradingSymbol.Remove(tradingSymbol.Count - 8)
                                Else
                                    instrumentName = tradingSymbol
                                End If
                                Dim detailsOfStock As StockDetails = New StockDetails With
                                            {.StockName = instrumentName,
                                            .LotSize = 1,
                                            .Supporting1 = If(direction.ToUpper = "BUY", 1, -1),
                                            .EligibleToTakeTrade = True}
                                ret.Add(instrumentName, detailsOfStock)
                            Next
                        Case 39
                            Dim stocksData As List(Of NiftyBankMarketPairTradingStrategyRule.StockData) = Nothing
                            For i = 1 To dt.Rows.Count - 1
                                Dim rowDate As Date = dt.Rows(i)(0)
                                If rowDate.Date = tradingDate.Date Then
                                    Dim tradingSymbol As String = dt.Rows(i).Item(1)
                                    Dim instrumentName As String = Nothing
                                    If tradingSymbol.Contains("FUT") Then
                                        instrumentName = tradingSymbol.Remove(tradingSymbol.Count - 8)
                                    Else
                                        instrumentName = tradingSymbol
                                    End If
                                    Dim detailsOfStock As NiftyBankMarketPairTradingStrategyRule.StockData = New NiftyBankMarketPairTradingStrategyRule.StockData With
                                                {.InstrumentName = instrumentName,
                                                 .LotSize = dt.Rows(i).Item(2),
                                                 .ChangePer = dt.Rows(i).Item(11)}

                                    If stocksData Is Nothing Then stocksData = New List(Of NiftyBankMarketPairTradingStrategyRule.StockData)
                                    stocksData.Add(detailsOfStock)
                                End If
                            Next
                            ret = NiftyBankMarketPairTradingStrategyRule.GetStockData(stocksData, CType(Me.RuleEntityData, NiftyBankMarketPairTradingStrategyRule.StrategyRuleEntities).StockSelectionDetails)
                        Case 40
                            Dim stocksData As List(Of FavourableFractalBreakoutStrategyRule.StockData) = Nothing
                            Dim previousTradingDay As Date = Cmn.GetPreviousTradingDay(Common.DataBaseTable.EOD_Cash, tradingDate)
                            For i = 1 To dt.Rows.Count - 1
                                Dim rowDate As Date = dt.Rows(i)(0)
                                If rowDate.Date = previousTradingDay.Date Then
                                    Dim tradingSymbol As String = dt.Rows(i).Item(1)
                                    Dim instrumentName As String = Nothing
                                    If tradingSymbol.Contains("FUT") Then
                                        instrumentName = tradingSymbol.Remove(tradingSymbol.Count - 8)
                                    Else
                                        instrumentName = tradingSymbol
                                    End If
                                    Dim detailsOfStock As FavourableFractalBreakoutStrategyRule.StockData = New FavourableFractalBreakoutStrategyRule.StockData With
                                                {.InstrumentName = instrumentName,
                                                 .LotSize = dt.Rows(i).Item(2),
                                                 .ChangePer = dt.Rows(i).Item(11)}

                                    If stocksData Is Nothing Then stocksData = New List(Of FavourableFractalBreakoutStrategyRule.StockData)
                                    stocksData.Add(detailsOfStock)
                                End If
                            Next
                            ret = FavourableFractalBreakoutStrategyRule.GetStockData(stocksData, Me.NumberOfTradeableStockPerDay)
                        Case 42
                            Dim counter As Integer = 0
                            For i = 1 To dt.Rows.Count - 1
                                Dim rowDate As Date = dt.Rows(i)(0)
                                If rowDate.Date = tradingDate.Date Then
                                    If ret Is Nothing Then ret = New Dictionary(Of String, StockDetails)
                                    Dim tradingSymbol As String = dt.Rows(i).Item(1)
                                    Dim instrumentName As String = Nothing
                                    If tradingSymbol.Contains("FUT") Then
                                        instrumentName = tradingSymbol.Remove(tradingSymbol.Count - 8)
                                    Else
                                        instrumentName = tradingSymbol
                                    End If
                                    Dim detailsOfStock As StockDetails = New StockDetails With
                                                {.StockName = instrumentName,
                                                .LotSize = dt.Rows(i).Item(2),
                                                .EligibleToTakeTrade = True}
                                    ret.Add(instrumentName, detailsOfStock)
                                    counter += 1
                                    If counter = Me.NumberOfTradeableStockPerDay Then Exit For
                                End If
                            Next
                        Case 49
                            Dim stockList As List(Of StockDetails) = Nothing
                            Dim previousTradingDay As Date = Cmn.GetPreviousTradingDay(Me.DatabaseTable, tradingDate)
                            For i = 1 To dt.Rows.Count - 1
                                Dim rowDate As Date = dt.Rows(i)(0)
                                If rowDate.Date = previousTradingDay.Date Then
                                    Dim tradingSymbol As String = dt.Rows(i).Item(1)
                                    Dim instrumentName As String = Nothing
                                    If tradingSymbol.Contains("FUT") Then
                                        instrumentName = tradingSymbol.Remove(tradingSymbol.Count - 8)
                                    Else
                                        instrumentName = tradingSymbol
                                    End If
                                    Dim detailsOfStock As StockDetails = New StockDetails With
                                                {.StockName = instrumentName,
                                                .LotSize = dt.Rows(i).Item(2),
                                                .EligibleToTakeTrade = True,
                                                .Supporting1 = dt.Rows(i).Item(11)}
                                    If stockList Is Nothing Then stockList = New List(Of StockDetails)
                                    stockList.Add(detailsOfStock)
                                End If
                            Next
                            If stockList IsNot Nothing AndAlso stockList.Count > 0 Then
                                Dim counter As Integer = 0
                                For Each runningStock In stockList.OrderByDescending(Function(x)
                                                                                         Return x.Supporting1
                                                                                     End Function)
                                    If ret Is Nothing Then ret = New Dictionary(Of String, StockDetails)
                                    ret.Add(runningStock.StockName, runningStock)
                                    counter += 1
                                    If counter = Me.NumberOfTradeableStockPerDay Then Exit For
                                Next
                                counter = 0
                                For Each runningStock In stockList.OrderBy(Function(x)
                                                                               Return x.Supporting1
                                                                           End Function)
                                    If ret Is Nothing Then ret = New Dictionary(Of String, StockDetails)
                                    ret.Add(runningStock.StockName, runningStock)
                                    counter += 1
                                    If counter = Me.NumberOfTradeableStockPerDay Then Exit For
                                Next
                            End If
                        Case 52, 53
                            Dim counter As Integer = 0
                            For i = 1 To dt.Rows.Count - 1
                                Dim rowDate As Date = dt.Rows(i)(0)
                                If rowDate.Date = tradingDate.Date Then
                                    If ret Is Nothing Then ret = New Dictionary(Of String, StockDetails)
                                    Dim tradingSymbol As String = dt.Rows(i).Item(1)
                                    Dim instrumentName As String = Nothing
                                    If tradingSymbol.Contains("FUT") Then
                                        instrumentName = tradingSymbol.Remove(tradingSymbol.Count - 8)
                                    Else
                                        instrumentName = tradingSymbol
                                    End If
                                    Dim detailsOfStock As StockDetails = New StockDetails With
                                                {.StockName = instrumentName,
                                                .LotSize = dt.Rows(i).Item(2),
                                                .EligibleToTakeTrade = True,
                                                .Supporting1 = dt.Rows(i).Item(10)}
                                    ret.Add(instrumentName, detailsOfStock)
                                    counter += 1
                                    If counter = Me.NumberOfTradeableStockPerDay Then Exit For
                                End If
                            Next
                        Case Else
                            Dim counter As Integer = 0
                            For i = 1 To dt.Rows.Count - 1
                                Dim rowDate As Date = dt.Rows(i)(0)
                                If rowDate.Date = tradingDate.Date Then
                                    If ret Is Nothing Then ret = New Dictionary(Of String, StockDetails)
                                    Dim tradingSymbol As String = dt.Rows(i).Item(1)
                                    Dim instrumentName As String = Nothing
                                    If tradingSymbol.Contains("FUT") Then
                                        instrumentName = tradingSymbol.Remove(tradingSymbol.Count - 8)
                                    Else
                                        instrumentName = tradingSymbol
                                    End If
                                    Dim detailsOfStock As StockDetails = New StockDetails With
                                                {.StockName = instrumentName,
                                                .LotSize = dt.Rows(i).Item(2),
                                                .EligibleToTakeTrade = True,
                                                .Supporting1 = dt.Rows(i).Item(3),
                                                .Supporting2 = dt.Rows(i).Item(5)}
                                    ret.Add(instrumentName, detailsOfStock)
                                    counter += 1
                                    If counter = Me.NumberOfTradeableStockPerDay Then Exit For
                                End If
                            Next
                    End Select
                End If
            End If
            Return ret
        End Function
#End Region

#Region "IDisposable Support"
        Private disposedValue As Boolean ' To detect redundant calls

        ' IDisposable
        Protected Overridable Sub Dispose(disposing As Boolean)
            If Not disposedValue Then
                If disposing Then
                    ' TODO: dispose managed state (managed objects).
                End If

                ' TODO: free unmanaged resources (unmanaged objects) and override Finalize() below.
                ' TODO: set large fields to null.
            End If
            disposedValue = True
        End Sub

        ' TODO: override Finalize() only if Dispose(disposing As Boolean) above has code to free unmanaged resources.
        'Protected Overrides Sub Finalize()
        '    ' Do not change this code.  Put cleanup code in Dispose(disposing As Boolean) above.
        '    Dispose(False)
        '    MyBase.Finalize()
        'End Sub

        ' This code added by Visual Basic to correctly implement the disposable pattern.
        Public Sub Dispose() Implements IDisposable.Dispose
            ' Do not change this code.  Put cleanup code in Dispose(disposing As Boolean) above.
            Dispose(True)
            ' TODO: uncomment the following line if Finalize() is overridden above.
            ' GC.SuppressFinalize(Me)
        End Sub
#End Region
    End Class
End Namespace