Imports System.Globalization

Public Class PageUiKitRight

    Private Shared ReadOnly HotkeyOptions As String() =
        Enumerable.Range(1, 12).Select(Function(index) $"F{index}").ToArray()
    Private Page As UiKitDemoPage = UiKitDemoPage.Scan
    Private EventsAttached As Boolean
    Private ConfigurationLoaded As Boolean
    Private StartRequestInProgress As Boolean

    Public Shared Function Create(page As UiKitDemoPage) As PageUiKitRight
        Dim result As New PageUiKitRight()
        result.Configure(page)
        Return result
    End Function

    Public Sub Configure(page As UiKitDemoPage)
        Me.Page = page

        Dim showScan = page = UiKitDemoPage.Scan
        Dim showSettings = page = UiKitDemoPage.Settings
        Dim showAbout = page = UiKitDemoPage.About
        CardScanActions.Visibility = If(showScan, Visibility.Visible, Visibility.Collapsed)
        CardLog.Visibility = If(showScan, Visibility.Visible, Visibility.Collapsed)
        CardRegions.Visibility = If(showSettings, Visibility.Visible, Visibility.Collapsed)
        CardParameters.Visibility = If(showSettings, Visibility.Visible, Visibility.Collapsed)
        CardOptions.Visibility = If(showSettings, Visibility.Visible, Visibility.Collapsed)
        CardAboutProject.Visibility = If(showAbout, Visibility.Visible, Visibility.Collapsed)
        CardInterface.Visibility = If(showAbout, Visibility.Visible, Visibility.Collapsed)
        CardLicenses.Visibility = If(showAbout, Visibility.Visible, Visibility.Collapsed)

        If showScan AndAlso FrmMain IsNot Nothing Then RefreshIdleText()
    End Sub

    Private Sub PageUiKitRight_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        Configure(Page)
        If Page = UiKitDemoPage.Scan AndAlso Not EventsAttached AndAlso FrmMain IsNot Nothing Then
            EventsAttached = True
            AddHandler FrmMain.Controller.LogReceived, AddressOf OnLogReceived
            AddHandler FrmMain.Controller.StatusChanged, AddressOf OnStatusChanged
            AddHandler FrmMain.Controller.ProgressChanged, AddressOf OnProgressChanged
            AddHandler FrmMain.Controller.ScanStateChanged, AddressOf OnScanStateChanged
            AddHandler FrmMain.Controller.BackendAvailabilityChanged, AddressOf OnBackendAvailabilityChanged
            AddHandler FrmMain.Controller.ConfigurationChanged, AddressOf OnConfigurationChanged
            AddHandler FrmMain.Controller.StartHotkeyPressed, AddressOf OnStartHotkeyPressed
            AddHandler FrmMain.Controller.StopHotkeyPressed, AddressOf OnStopHotkeyPressed
            SetBackendAvailability(FrmMain.Controller.IsBackendAvailable)
        End If
        If Page = UiKitDemoPage.Settings AndAlso Not ConfigurationLoaded AndAlso FrmMain IsNot Nothing Then
            LoadConfiguration(FrmMain.Controller.Configuration)
        End If
    End Sub

    Public Sub LoadConfiguration(config As ScannerConfiguration)
        If Page <> UiKitDemoPage.Settings OrElse config Is Nothing Then Return
        ConfigurationLoaded = True
        LabScreenSize.Text = $"当前主屏幕：{config.ScreenW} × {config.ScreenH}"
        TxtScanX.Text = config.ScanX.ToString(CultureInfo.InvariantCulture)
        TxtScanY.Text = config.ScanY.ToString(CultureInfo.InvariantCulture)
        TxtScanW.Text = config.ScanW.ToString(CultureInfo.InvariantCulture)
        TxtScanH.Text = config.ScanH.ToString(CultureInfo.InvariantCulture)
        TxtDetectX.Text = config.DetectX.ToString(CultureInfo.InvariantCulture)
        TxtDetectY.Text = config.DetectY.ToString(CultureInfo.InvariantCulture)
        TxtDetectW.Text = config.DetectW.ToString(CultureInfo.InvariantCulture)
        TxtDetectH.Text = config.DetectH.ToString(CultureInfo.InvariantCulture)
        TxtStepX.Text = config.StepX.ToString(CultureInfo.InvariantCulture)
        TxtStepY.Text = config.StepY.ToString(CultureInfo.InvariantCulture)
        TxtMoveDelay.Text = config.MoveDelay.ToString(CultureInfo.InvariantCulture)
        TxtDiffThreshold.Text = config.DiffThreshold.ToString(CultureInfo.InvariantCulture)
        TxtStartDelay.Text = config.StartDelay.ToString(CultureInfo.InvariantCulture)
        ChkStopOnHit.SetChecked(config.StopOnHit, False)
        ChkAutoSave.SetChecked(config.AutoSave, False)
        ComboStartHotkey.ItemsSource = HotkeyOptions
        ComboStopHotkey.ItemsSource = HotkeyOptions
        ComboStartHotkey.SelectedItem = NormalizeHotkey(config.StartHotkey, "F7")
        ComboStopHotkey.SelectedItem = NormalizeHotkey(config.StopHotkey, "F8")
    End Sub

    Public Function ReadConfiguration() As ScannerConfiguration
        If Page <> UiKitDemoPage.Settings Then Throw New InvalidOperationException("设置页面不可用。")
        Dim screenW = FrmMain.Controller.Configuration.ScreenW
        Dim screenH = FrmMain.Controller.Configuration.ScreenH
        Dim result As New ScannerConfiguration With {
            .ScreenW = screenW,
            .ScreenH = screenH,
            .ScanX = ParseInteger(TxtScanX, "扫描区域 X 起点"),
            .ScanY = ParseInteger(TxtScanY, "扫描区域 Y 起点"),
            .ScanW = ParseInteger(TxtScanW, "扫描区域宽度"),
            .ScanH = ParseInteger(TxtScanH, "扫描区域高度"),
            .DetectX = ParseInteger(TxtDetectX, "检测区域 X 起点"),
            .DetectY = ParseInteger(TxtDetectY, "检测区域 Y 起点"),
            .DetectW = ParseInteger(TxtDetectW, "检测区域宽度"),
            .DetectH = ParseInteger(TxtDetectH, "检测区域高度"),
            .StepX = ParseInteger(TxtStepX, "横向步长"),
            .StepY = ParseInteger(TxtStepY, "纵向步长"),
            .MoveDelay = ParseDouble(TxtMoveDelay, "移动延迟"),
            .DiffThreshold = ParseDouble(TxtDiffThreshold, "差异阈值"),
            .StartDelay = ParseDouble(TxtStartDelay, "开始延迟"),
            .StopOnHit = ChkStopOnHit.Checked,
            .AutoSave = ChkAutoSave.Checked,
            .StartHotkey = CStr(ComboStartHotkey.SelectedItem),
            .StopHotkey = CStr(ComboStopHotkey.SelectedItem)
        }

        If result.ScanX < 0 OrElse result.ScanY < 0 OrElse result.DetectX < 0 OrElse result.DetectY < 0 Then
            Throw New ArgumentException("区域起点不能为负数。")
        End If
        If result.ScanW <= 0 OrElse result.ScanH <= 0 OrElse result.DetectW <= 0 OrElse result.DetectH <= 0 Then
            Throw New ArgumentException("扫描区域和检测区域的宽度、高度必须大于 0。")
        End If
        If result.StepX <= 0 OrElse result.StepY <= 0 Then Throw New ArgumentException("扫描步长必须大于 0。")
        If result.MoveDelay < 0 OrElse result.DiffThreshold < 0 OrElse result.StartDelay < 0 Then
            Throw New ArgumentException("延迟和差异阈值不能为负数。")
        End If
        If result.ScanX + result.ScanW > screenW OrElse result.ScanY + result.ScanH > screenH Then
            Throw New ArgumentException("扫描区域超出当前主屏幕范围。")
        End If
        If result.DetectX + result.DetectW > screenW OrElse result.DetectY + result.DetectH > screenH Then
            Throw New ArgumentException("检测区域超出当前主屏幕范围。")
        End If
        If Not HotkeyOptions.Contains(result.StartHotkey) OrElse Not HotkeyOptions.Contains(result.StopHotkey) Then
            Throw New ArgumentException("开始和停止快捷键必须从 F1 到 F12 中选择。")
        End If
        If result.StartHotkey = result.StopHotkey Then Throw New ArgumentException("开始和停止快捷键不能相同。")
        Return result
    End Function

    Private Shared Function NormalizeHotkey(value As String, fallback As String) As String
        Dim normalized = If(value, "").Trim().ToUpperInvariant()
        Return If(HotkeyOptions.Contains(normalized), normalized, fallback)
    End Function

    Private Shared Function ParseInteger(input As MyTextBox, label As String) As Integer
        Dim value As Integer
        If Not Integer.TryParse(input.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, value) Then
            Throw New ArgumentException(label & "必须是整数。")
        End If
        Return value
    End Function

    Private Shared Function ParseDouble(input As MyTextBox, label As String) As Double
        Dim value As Double
        If Double.TryParse(input.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, value) OrElse
           Double.TryParse(input.Text.Trim(), NumberStyles.Float, CultureInfo.CurrentCulture, value) Then
            Return value
        End If
        Throw New ArgumentException(label & "必须是数字。")
    End Function

    Private Async Sub BtnCaptureTemplate_Click(sender As Object, e As EventArgs) Handles BtnCaptureTemplate.Click
        Dim config As ScannerConfiguration = Nothing
        If Not FrmMain.TryGetCurrentConfiguration(config) Then Return
        SetActionButtons(False)
        Try
            Dim path = Await FrmMain.Controller.CaptureTemplateAsync(config)
            Hint("模板已保存。", HintType.Green)
            MyMsgBox("模板已保存：" & path & vbCrLf & vbCrLf & "建议确认模板准确截到了快速移动提示。", "模板已保存")
        Catch ex As Exception
            MyMsgBox(ex.Message, "截取失败", IsWarn:=True)
        Finally
            SetActionButtons(True)
        End Try
    End Sub

    Private Async Sub BtnTestDiff_Click(sender As Object, e As EventArgs) Handles BtnTestDiff.Click
        Dim config As ScannerConfiguration = Nothing
        If Not FrmMain.TryGetCurrentConfiguration(config) Then Return
        SetActionButtons(False)
        Try
            Dim score = Await FrmMain.Controller.TestDiffAsync(config)
            Hint($"当前差异分数：{score:0.00}", HintType.Green)
            MyMsgBox($"当前差异分数：{score:0.00}" & vbCrLf & vbCrLf & "鼠标位于已探索道路时，该分数应保持较低。", "测试结果")
        Catch ex As Exception
            MyMsgBox(ex.Message, "测试失败", IsWarn:=True)
        Finally
            SetActionButtons(True)
        End Try
    End Sub

    Private Async Sub BtnStartScan_Click(sender As Object, e As EventArgs) Handles BtnStartScan.Click
        Await StartScanAsync()
    End Sub

    Public Async Function StartScanAsync() As Task
        If StartRequestInProgress OrElse FrmMain.Controller.IsScanning Then Return
        StartRequestInProgress = True
        Dim config As ScannerConfiguration = Nothing
        If Not FrmMain.TryGetCurrentConfiguration(config) Then
            StartRequestInProgress = False
            Return
        End If
        Try
            ScanProgress.Value = 0
            LabProgress.Text = "进度 0%"
            Await FrmMain.Controller.StartScanAsync(config)
        Catch ex As Exception
            MyMsgBox(ex.Message, "无法开始扫描", IsWarn:=True)
        Finally
            StartRequestInProgress = False
        End Try
    End Function

    Private Async Sub BtnStopScan_Click(sender As Object, e As EventArgs) Handles BtnStopScan.Click
        Await StopScanAsync()
    End Sub

    Public Async Function StopScanAsync() As Task
        If Not FrmMain.Controller.IsScanning Then Return
        BtnStopScan.IsEnabled = False
        Try
            Await FrmMain.Controller.StopScanAsync()
        Catch ex As Exception
            MyMsgBox(ex.Message, "停止失败", IsWarn:=True)
        End Try
    End Function

    Private Sub BtnOpenHits_Click(sender As Object, e As EventArgs) Handles BtnOpenHits.Click
        FrmMain.Controller.OpenHitFolder()
    End Sub

    Private Sub BtnSaveConfig_Click(sender As Object, e As EventArgs) Handles BtnSaveConfig.Click
        Dim config As ScannerConfiguration = Nothing
        If Not FrmMain.TryGetCurrentConfiguration(config, True) Then Return
        Hint("配置已保存。", HintType.Green)
    End Sub

    Private Sub BtnRestoreDefaults_Click(sender As Object, e As EventArgs) Handles BtnRestoreDefaults.Click
        Dim current = FrmMain.Controller.Configuration
        Dim defaults = ScannerConfiguration.CreateDefault(current.ScreenW, current.ScreenH)
        LoadConfiguration(defaults)
        Hint("已恢复默认值，点击保存配置后写入磁盘。")
    End Sub

    Private Sub BtnOpenAuthor_Click(sender As Object, e As EventArgs) Handles BtnOpenAuthor.Click
        OpenWebsite("https://space.bilibili.com/9997742")
    End Sub

    Private Sub BtnOpenRepository_Click(sender As Object, e As EventArgs) Handles BtnOpenRepository.Click
        OpenWebsite("https://github.com/Dr-hydra/FH6-Road-Scanner")
    End Sub

    Private Sub BtnOpenUiKit_Click(sender As Object, e As EventArgs) Handles BtnOpenUiKit.Click
        OpenWebsite("https://github.com/Dr-hydra/QING.UIKIT")
    End Sub

    Private Sub BtnOpenPcl_Click(sender As Object, e As EventArgs) Handles BtnOpenPcl.Click
        OpenWebsite("https://github.com/Meloong-Git/PCL")
    End Sub

    Private Sub BtnOpenLicenses_Click(sender As Object, e As EventArgs) Handles BtnOpenLicenses.Click
        FrmMain.Controller.OpenLicensesFolder()
    End Sub

    Private Sub SetActionButtons(enabled As Boolean)
        BtnCaptureTemplate.IsEnabled = enabled
        BtnTestDiff.IsEnabled = enabled
    End Sub

    Private Sub OnLogReceived(text As String)
        TxtLog.AppendText(text & Environment.NewLine)
        TxtLog.ScrollToEnd()
    End Sub

    Private Sub OnStatusChanged(text As String)
        LabScanDetail.Text = text
    End Sub

    Private Sub OnProgressChanged(value As Double)
        ScanProgress.Value = Math.Max(0, Math.Min(100, value))
        LabProgress.Text = $"进度 {ScanProgress.Value:0}%"
    End Sub

    Private Sub OnScanStateChanged(isRunning As Boolean, stateText As String)
        BtnStartScan.IsEnabled = Not isRunning
        BtnStopScan.IsEnabled = isRunning
        BtnCaptureTemplate.IsEnabled = Not isRunning
        BtnTestDiff.IsEnabled = Not isRunning
        LabScanState.Text = stateText
        If Not isRunning AndAlso stateText = "扫描完成" Then
            ScanProgress.Value = 100
            LabProgress.Text = "进度 100%"
        End If
        If Not isRunning Then RefreshIdleText()
    End Sub

    Private Sub OnBackendAvailabilityChanged(available As Boolean)
        SetBackendAvailability(available)
    End Sub

    Private Sub OnConfigurationChanged(config As ScannerConfiguration)
        RefreshIdleText()
    End Sub

    Public Sub SetBackendAvailability(available As Boolean)
        If Page <> UiKitDemoPage.Scan Then Return
        BtnStartScan.IsEnabled = available
        BtnCaptureTemplate.IsEnabled = available
        BtnTestDiff.IsEnabled = available
        If available Then
            If LabScanState.Text = "后端不可用" Then LabScanState.Text = "就绪"
            RefreshIdleText()
        Else
            LabScanDetail.Text = "扫描后端已断开。"
            LabScanState.Text = "后端不可用"
            BtnStopScan.IsEnabled = False
        End If
    End Sub

    Private Async Sub OnStartHotkeyPressed()
        Await StartScanAsync()
    End Sub

    Private Async Sub OnStopHotkeyPressed()
        Await StopScanAsync()
    End Sub

    Public Sub RefreshIdleText()
        If Page <> UiKitDemoPage.Scan OrElse FrmMain Is Nothing Then Return
        If FrmMain.Controller.IsScanning Then Return
        Dim config = FrmMain.Controller.Configuration
        LabScanState.Text = If(FrmMain.Controller.IsBackendAvailable, "就绪", "连接中")
        LabScanDetail.Text = $"按 {config.StartHotkey} 开始扫描，按 {config.StopHotkey} 停止扫描。"
    End Sub

End Class
