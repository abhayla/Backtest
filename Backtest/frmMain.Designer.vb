﻿<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class frmMain
    Inherits System.Windows.Forms.Form

    'Form overrides dispose to clean up the component list.
    <System.Diagnostics.DebuggerNonUserCode()> _
    Protected Overrides Sub Dispose(ByVal disposing As Boolean)
        Try
            If disposing AndAlso components IsNot Nothing Then
                components.Dispose()
            End If
        Finally
            MyBase.Dispose(disposing)
        End Try
    End Sub

    'Required by the Windows Form Designer
    Private components As System.ComponentModel.IContainer

    'NOTE: The following procedure is required by the Windows Form Designer
    'It can be modified using the Windows Form Designer.  
    'Do not modify it using the code editor.
    <System.Diagnostics.DebuggerStepThrough()> _
    Private Sub InitializeComponent()
        Dim resources As System.ComponentModel.ComponentResourceManager = New System.ComponentModel.ComponentResourceManager(GetType(frmMain))
        Me.btnStart = New System.Windows.Forms.Button()
        Me.lblProgress = New System.Windows.Forms.Label()
        Me.grpbxDataSource = New System.Windows.Forms.GroupBox()
        Me.rdbLive = New System.Windows.Forms.RadioButton()
        Me.rdbDatabase = New System.Windows.Forms.RadioButton()
        Me.cmbRule = New System.Windows.Forms.ComboBox()
        Me.lblChooseRule = New System.Windows.Forms.Label()
        Me.lblStartDate = New System.Windows.Forms.Label()
        Me.lblEndDate = New System.Windows.Forms.Label()
        Me.dtpckrStartDate = New System.Windows.Forms.DateTimePicker()
        Me.dtpckrEndDate = New System.Windows.Forms.DateTimePicker()
        Me.btnStop = New System.Windows.Forms.Button()
        Me.grpbxStrategyType = New System.Windows.Forms.GroupBox()
        Me.rdbCNCEOD = New System.Windows.Forms.RadioButton()
        Me.rdbCNCCandle = New System.Windows.Forms.RadioButton()
        Me.rdbCNCTick = New System.Windows.Forms.RadioButton()
        Me.rdbMIS = New System.Windows.Forms.RadioButton()
        Me.grpbxDataSource.SuspendLayout()
        Me.grpbxStrategyType.SuspendLayout()
        Me.SuspendLayout()
        '
        'btnStart
        '
        Me.btnStart.Font = New System.Drawing.Font("Microsoft Sans Serif", 12.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.btnStart.Location = New System.Drawing.Point(113, 115)
        Me.btnStart.Margin = New System.Windows.Forms.Padding(2, 2, 2, 2)
        Me.btnStart.Name = "btnStart"
        Me.btnStart.Size = New System.Drawing.Size(136, 46)
        Me.btnStart.TabIndex = 0
        Me.btnStart.Text = "Start"
        Me.btnStart.UseVisualStyleBackColor = True
        '
        'lblProgress
        '
        Me.lblProgress.Location = New System.Drawing.Point(5, 187)
        Me.lblProgress.Margin = New System.Windows.Forms.Padding(2, 0, 2, 0)
        Me.lblProgress.Name = "lblProgress"
        Me.lblProgress.Size = New System.Drawing.Size(499, 41)
        Me.lblProgress.TabIndex = 1
        Me.lblProgress.Text = "Progress Status ....."
        '
        'grpbxDataSource
        '
        Me.grpbxDataSource.Controls.Add(Me.rdbLive)
        Me.grpbxDataSource.Controls.Add(Me.rdbDatabase)
        Me.grpbxDataSource.Location = New System.Drawing.Point(357, 7)
        Me.grpbxDataSource.Margin = New System.Windows.Forms.Padding(2, 2, 2, 2)
        Me.grpbxDataSource.Name = "grpbxDataSource"
        Me.grpbxDataSource.Padding = New System.Windows.Forms.Padding(2, 2, 2, 2)
        Me.grpbxDataSource.Size = New System.Drawing.Size(140, 46)
        Me.grpbxDataSource.TabIndex = 24
        Me.grpbxDataSource.TabStop = False
        Me.grpbxDataSource.Text = "Data Source"
        '
        'rdbLive
        '
        Me.rdbLive.AutoSize = True
        Me.rdbLive.Location = New System.Drawing.Point(86, 20)
        Me.rdbLive.Margin = New System.Windows.Forms.Padding(2, 2, 2, 2)
        Me.rdbLive.Name = "rdbLive"
        Me.rdbLive.Size = New System.Drawing.Size(45, 17)
        Me.rdbLive.TabIndex = 1
        Me.rdbLive.Text = "Live"
        Me.rdbLive.UseVisualStyleBackColor = True
        '
        'rdbDatabase
        '
        Me.rdbDatabase.AutoSize = True
        Me.rdbDatabase.Checked = True
        Me.rdbDatabase.Location = New System.Drawing.Point(5, 19)
        Me.rdbDatabase.Margin = New System.Windows.Forms.Padding(2, 2, 2, 2)
        Me.rdbDatabase.Name = "rdbDatabase"
        Me.rdbDatabase.Size = New System.Drawing.Size(71, 17)
        Me.rdbDatabase.TabIndex = 0
        Me.rdbDatabase.TabStop = True
        Me.rdbDatabase.Text = "Database"
        Me.rdbDatabase.UseVisualStyleBackColor = True
        '
        'cmbRule
        '
        Me.cmbRule.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.cmbRule.FormattingEnabled = True
        Me.cmbRule.Items.AddRange(New Object() {"Smallest Candle Breakout", "High Volume Pin Bar", "Momentum Reversal v2", "High Volume Pin Bar v2", "Donchian Fractal Breakout", "SMI Fractal Breakout", "Day Long SMI (BANKNIFTY)", "Day Start SMI", "Gap Fractal Breakout", "Forward Momentum", "Vijay CNC", "TII Opposite Breakout", "Fixed Level Based", "Low Stoploss", "Multi Target", "Reversal", "Pinbar Breakout", "Low SL Pinbar", "Investment CNC", "Low Stoploss Wick", "Low Stoploss Candle", "Pair Trading", "Coin Flip At Resistance", "HeikenAshi CNC", "HeikenAshi CNC-1", "SMI HeikenAshi CNC", "HeikenAshi Hourly CNC-1", "ATR CNC", "Price Drop CNC", "Trend Line CNC", "Pivot Point Direction Biased", "TII CNC", "Intraday Positional", "Double Top Double Bottom", "Hourly CNC", "Intraday Positional 2", "Intraday Positional 3", "Intraday Positional 4", "Price Drop Continues", "Nifty Bank Market Pair Trading", "Favourable Fractal Breakout", "Fractal Dip", "CRUDEOIL EOD", "Highest Price Drop Continues", "Outside Bollinger", "Favourable Fractal Breakout 2", "Average Price Drop Continues", "Swing CNC", "HK Positional Hourly Strategy 2", "HK ATR Trailling", "RSI Continues Strategy", "HK RSI Continues Strategy", "HK Slab Level Based", "High Low Slab Level Based", "Previous Day Factor", "High Low EMA"})
        Me.cmbRule.Location = New System.Drawing.Point(92, 24)
        Me.cmbRule.Margin = New System.Windows.Forms.Padding(2, 2, 2, 2)
        Me.cmbRule.Name = "cmbRule"
        Me.cmbRule.Size = New System.Drawing.Size(251, 23)
        Me.cmbRule.TabIndex = 22
        '
        'lblChooseRule
        '
        Me.lblChooseRule.AutoSize = True
        Me.lblChooseRule.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.lblChooseRule.Location = New System.Drawing.Point(9, 27)
        Me.lblChooseRule.Margin = New System.Windows.Forms.Padding(2, 0, 2, 0)
        Me.lblChooseRule.Name = "lblChooseRule"
        Me.lblChooseRule.Size = New System.Drawing.Size(78, 15)
        Me.lblChooseRule.TabIndex = 23
        Me.lblChooseRule.Text = "Choose Rule"
        '
        'lblStartDate
        '
        Me.lblStartDate.AutoSize = True
        Me.lblStartDate.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.lblStartDate.Location = New System.Drawing.Point(9, 72)
        Me.lblStartDate.Margin = New System.Windows.Forms.Padding(2, 0, 2, 0)
        Me.lblStartDate.Name = "lblStartDate"
        Me.lblStartDate.Size = New System.Drawing.Size(61, 15)
        Me.lblStartDate.TabIndex = 25
        Me.lblStartDate.Text = "Start Date"
        '
        'lblEndDate
        '
        Me.lblEndDate.AutoSize = True
        Me.lblEndDate.Font = New System.Drawing.Font("Microsoft Sans Serif", 9.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.lblEndDate.Location = New System.Drawing.Point(181, 72)
        Me.lblEndDate.Margin = New System.Windows.Forms.Padding(2, 0, 2, 0)
        Me.lblEndDate.Name = "lblEndDate"
        Me.lblEndDate.Size = New System.Drawing.Size(58, 15)
        Me.lblEndDate.TabIndex = 26
        Me.lblEndDate.Text = "End Date"
        '
        'dtpckrStartDate
        '
        Me.dtpckrStartDate.Format = System.Windows.Forms.DateTimePickerFormat.[Short]
        Me.dtpckrStartDate.Location = New System.Drawing.Point(72, 70)
        Me.dtpckrStartDate.Name = "dtpckrStartDate"
        Me.dtpckrStartDate.Size = New System.Drawing.Size(104, 20)
        Me.dtpckrStartDate.TabIndex = 27
        '
        'dtpckrEndDate
        '
        Me.dtpckrEndDate.Format = System.Windows.Forms.DateTimePickerFormat.[Short]
        Me.dtpckrEndDate.Location = New System.Drawing.Point(241, 70)
        Me.dtpckrEndDate.Name = "dtpckrEndDate"
        Me.dtpckrEndDate.Size = New System.Drawing.Size(104, 20)
        Me.dtpckrEndDate.TabIndex = 28
        '
        'btnStop
        '
        Me.btnStop.Font = New System.Drawing.Font("Microsoft Sans Serif", 12.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.btnStop.Location = New System.Drawing.Point(255, 115)
        Me.btnStop.Margin = New System.Windows.Forms.Padding(2, 2, 2, 2)
        Me.btnStop.Name = "btnStop"
        Me.btnStop.Size = New System.Drawing.Size(136, 46)
        Me.btnStop.TabIndex = 1
        Me.btnStop.Text = "Stop"
        Me.btnStop.UseVisualStyleBackColor = True
        '
        'grpbxStrategyType
        '
        Me.grpbxStrategyType.Controls.Add(Me.rdbCNCEOD)
        Me.grpbxStrategyType.Controls.Add(Me.rdbCNCCandle)
        Me.grpbxStrategyType.Controls.Add(Me.rdbCNCTick)
        Me.grpbxStrategyType.Controls.Add(Me.rdbMIS)
        Me.grpbxStrategyType.Location = New System.Drawing.Point(357, 54)
        Me.grpbxStrategyType.Margin = New System.Windows.Forms.Padding(2, 2, 2, 2)
        Me.grpbxStrategyType.Name = "grpbxStrategyType"
        Me.grpbxStrategyType.Padding = New System.Windows.Forms.Padding(2, 2, 2, 2)
        Me.grpbxStrategyType.Size = New System.Drawing.Size(140, 58)
        Me.grpbxStrategyType.TabIndex = 29
        Me.grpbxStrategyType.TabStop = False
        Me.grpbxStrategyType.Text = "Strategy Type"
        '
        'rdbCNCEOD
        '
        Me.rdbCNCEOD.AutoSize = True
        Me.rdbCNCEOD.Location = New System.Drawing.Point(73, 37)
        Me.rdbCNCEOD.Margin = New System.Windows.Forms.Padding(2, 2, 2, 2)
        Me.rdbCNCEOD.Name = "rdbCNCEOD"
        Me.rdbCNCEOD.Size = New System.Drawing.Size(73, 17)
        Me.rdbCNCEOD.TabIndex = 3
        Me.rdbCNCEOD.Text = "CNC EOD"
        Me.rdbCNCEOD.UseVisualStyleBackColor = True
        '
        'rdbCNCCandle
        '
        Me.rdbCNCCandle.AutoSize = True
        Me.rdbCNCCandle.Location = New System.Drawing.Point(5, 37)
        Me.rdbCNCCandle.Margin = New System.Windows.Forms.Padding(2, 2, 2, 2)
        Me.rdbCNCCandle.Name = "rdbCNCCandle"
        Me.rdbCNCCandle.Size = New System.Drawing.Size(71, 17)
        Me.rdbCNCCandle.TabIndex = 2
        Me.rdbCNCCandle.Text = "CNC Cndl"
        Me.rdbCNCCandle.UseVisualStyleBackColor = True
        '
        'rdbCNCTick
        '
        Me.rdbCNCTick.AutoSize = True
        Me.rdbCNCTick.Location = New System.Drawing.Point(72, 18)
        Me.rdbCNCTick.Margin = New System.Windows.Forms.Padding(2, 2, 2, 2)
        Me.rdbCNCTick.Name = "rdbCNCTick"
        Me.rdbCNCTick.Size = New System.Drawing.Size(71, 17)
        Me.rdbCNCTick.TabIndex = 1
        Me.rdbCNCTick.Text = "CNC Tick"
        Me.rdbCNCTick.UseVisualStyleBackColor = True
        '
        'rdbMIS
        '
        Me.rdbMIS.AutoSize = True
        Me.rdbMIS.Checked = True
        Me.rdbMIS.Location = New System.Drawing.Point(5, 16)
        Me.rdbMIS.Margin = New System.Windows.Forms.Padding(2, 2, 2, 2)
        Me.rdbMIS.Name = "rdbMIS"
        Me.rdbMIS.Size = New System.Drawing.Size(44, 17)
        Me.rdbMIS.TabIndex = 0
        Me.rdbMIS.TabStop = True
        Me.rdbMIS.Text = "MIS"
        Me.rdbMIS.UseVisualStyleBackColor = True
        '
        'frmMain
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(6.0!, 13.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.ClientSize = New System.Drawing.Size(509, 238)
        Me.Controls.Add(Me.grpbxStrategyType)
        Me.Controls.Add(Me.btnStop)
        Me.Controls.Add(Me.dtpckrEndDate)
        Me.Controls.Add(Me.dtpckrStartDate)
        Me.Controls.Add(Me.lblEndDate)
        Me.Controls.Add(Me.lblStartDate)
        Me.Controls.Add(Me.grpbxDataSource)
        Me.Controls.Add(Me.cmbRule)
        Me.Controls.Add(Me.lblChooseRule)
        Me.Controls.Add(Me.lblProgress)
        Me.Controls.Add(Me.btnStart)
        Me.Icon = CType(resources.GetObject("$this.Icon"), System.Drawing.Icon)
        Me.Margin = New System.Windows.Forms.Padding(2, 2, 2, 2)
        Me.Name = "frmMain"
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen
        Me.Text = "Algo2Trade Backtest"
        Me.grpbxDataSource.ResumeLayout(False)
        Me.grpbxDataSource.PerformLayout()
        Me.grpbxStrategyType.ResumeLayout(False)
        Me.grpbxStrategyType.PerformLayout()
        Me.ResumeLayout(False)
        Me.PerformLayout()

    End Sub

    Friend WithEvents btnStart As Button
    Friend WithEvents lblProgress As Label
    Friend WithEvents grpbxDataSource As GroupBox
    Friend WithEvents rdbLive As RadioButton
    Friend WithEvents rdbDatabase As RadioButton
    Friend WithEvents cmbRule As ComboBox
    Friend WithEvents lblChooseRule As Label
    Friend WithEvents lblStartDate As Label
    Friend WithEvents lblEndDate As Label
    Friend WithEvents dtpckrStartDate As DateTimePicker
    Friend WithEvents dtpckrEndDate As DateTimePicker
    Friend WithEvents btnStop As Button
    Friend WithEvents grpbxStrategyType As GroupBox
    Friend WithEvents rdbCNCTick As RadioButton
    Friend WithEvents rdbMIS As RadioButton
    Friend WithEvents rdbCNCEOD As RadioButton
    Friend WithEvents rdbCNCCandle As RadioButton
End Class
