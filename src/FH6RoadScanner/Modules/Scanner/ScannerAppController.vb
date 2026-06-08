Imports System.Globalization
Imports System.Text.Json

Public Class ScannerAppController
    Implements IDisposable

    Private ReadOnly Backend As New BackendClient()
    Private ReadOnly ConfigService As ScannerConfigService
    Private ReadOnly Hotkeys As New GlobalHotkeyService()
    Private _Configuration As ScannerConfiguration
    Private IsDisposed As Boolean

    Public Property Configuration As ScannerConfiguration
        Get
            Return _Configuration
        End Get
        Set(value As ScannerConfiguration)
            If value Is Nothing Then Throw New ArgumentNullException(NameOf(value))
            _Configuration = value
            Hotkeys.Configure(value.StartHotkey, value.StopHotkey)
            RaiseEvent ConfigurationChanged(value)
        End Set
    End Property
    Public Property IsScanning As Boolean
        Get
            Return _IsScanning
        End Get
        Private Set(value As Boolean)
            _IsScanning = value
        End Set
    End Property
    Private _IsScanning As Boolean
    Public ReadOnly Property IsBackendAvailable As Boolean
        Get
            Return Backend.IsConnected
        End Get
    End Property

    Public Event LogReceived(text As String)
    Public Event StatusChanged(text As String)
    Public Event ProgressChanged(value As Double)
    Public Event ScanStateChanged(isRunning As Boolean, stateText As String)
    Public Event BackendAvailabilityChanged(available As Boolean)
    Public Event ConfigurationChanged(config As ScannerConfiguration)
    Public Event StartHotkeyPressed()
    Public Event StopHotkeyPressed()

    Public Sub New()
        Dim bounds = System.Windows.Forms.Screen.PrimaryScreen.Bounds
        ConfigService = New ScannerConfigService(PathExeFolder, bounds.Width, bounds.Height)
        Configuration = ConfigService.Load()
        AddHandler Backend.EventReceived, AddressOf OnBackendEvent
        AddHandler Backend.DiagnosticReceived, Sub(text) AppendLocalLog("[后端] " & text)
        AddHandler Backend.ConnectionChanged, Sub(connected)
                                                  RunInUi(Sub() RaiseEvent BackendAvailabilityChanged(connected))
                                              End Sub
        AddHandler Hotkeys.StartPressed, AddressOf OnStartHotkeyPressed
        AddHandler Hotkeys.StopPressed, AddressOf OnStopHotkeyPressed
        Hotkeys.Start()
    End Sub

    Public Async Function InitializeAsync() As Task
        Await Backend.StartAsync()
        AppendLocalLog("本软件完全免费，请勿用于售卖或倒卖。")
        AppendLocalLog($"当前主屏幕：{Configuration.ScreenW} × {Configuration.ScreenH}")
    End Function

    Public Sub SaveConfiguration()
        ConfigService.Save(Configuration)
        AppendLocalLog("配置已保存：" & IO.Path.Combine(PathExeFolder, "config.json"))
    End Sub

    Public Async Function CaptureTemplateAsync(config As ScannerConfiguration) As Task(Of String)
        Dim result = Await Backend.SendCommandAsync("capture_template", New With {.detect_region = config.DetectRegion})
        Return GetString(result, "path")
    End Function

    Public Async Function TestDiffAsync(config As ScannerConfiguration) As Task(Of Double)
        Dim result = Await Backend.SendCommandAsync("test_diff", New With {.detect_region = config.DetectRegion})
        Return GetDouble(result, "score")
    End Function

    Public Async Function StartScanAsync(config As ScannerConfiguration) As Task
        Dim args = New With {
            .scan_region = config.ScanRegion,
            .detect_region = config.DetectRegion,
            .step_x = config.StepX,
            .step_y = config.StepY,
            .move_delay = config.MoveDelay,
            .diff_threshold = config.DiffThreshold,
            .start_delay = config.StartDelay,
            .stop_on_hit = config.StopOnHit
        }
        Await Backend.SendCommandAsync("start_scan", args)
    End Function

    Public Async Function StopScanAsync() As Task
        Await Backend.SendCommandAsync("stop_scan")
    End Function

    Public Sub OpenHitFolder()
        Dim path = IO.Path.Combine(PathExeFolder, "scan_hits")
        IO.Directory.CreateDirectory(path)
        OpenExplorer(path)
    End Sub

    Public Sub OpenLicensesFolder()
        Dim path = IO.Path.Combine(PathExeFolder, "LICENSES")
        If IO.Directory.Exists(path) Then
            OpenExplorer(path)
        Else
            MyMsgBox("当前运行目录中未找到 LICENSES 文件夹。", "许可证", IsWarn:=True)
        End If
    End Sub

    Public Sub AppendLocalLog(text As String)
        Dim line = $"[{Date.Now:HH:mm:ss}] {text}"
        RunInUi(Sub() RaiseEvent LogReceived(line))
    End Sub

    Public Async Function ShutdownAsync() As Task
        If IsDisposed Then Return
        Await Backend.ShutdownAsync()
        Dispose()
    End Function

    Private Sub OnBackendEvent(message As JsonElement)
        Dim eventValue As JsonElement
        If Not message.TryGetProperty("event", eventValue) Then Return
        Dim eventName = eventValue.GetString()
        RunInUi(
            Sub()
                Select Case eventName
                    Case "log"
                        RaiseEvent LogReceived($"[{Date.Now:HH:mm:ss}] {GetString(message, "text")}")
                    Case "status"
                        RaiseEvent StatusChanged(GetString(message, "text"))
                    Case "progress"
                        RaiseEvent ProgressChanged(GetDouble(message, "value"))
                    Case "scan_state"
                        Dim state = GetString(message, "state")
                        IsScanning = state = "running" OrElse state = "stopping"
                        RaiseEvent ScanStateChanged(IsScanning, StateToText(state))
                    Case "hit"
                        RaiseEvent StatusChanged($"命中：x={GetInt(message, "x")}, y={GetInt(message, "y")}, 差异={GetDouble(message, "score"):0.00}")
                End Select
            End Sub)
    End Sub

    Private Shared Function StateToText(state As String) As String
        Select Case state
            Case "running"
                Return "扫描中"
            Case "stopping"
                Return "正在停止"
            Case "completed"
                Return "扫描完成"
            Case "error"
                Return "扫描出错"
            Case "stopped"
                Return "扫描已停止"
            Case Else
                Return "就绪"
        End Select
    End Function

    Private Sub OnStartHotkeyPressed()
        If IsBackendAvailable AndAlso Not IsScanning Then
            RunInUi(Sub() RaiseEvent StartHotkeyPressed())
        End If
    End Sub

    Private Sub OnStopHotkeyPressed()
        If IsScanning Then RunInUi(Sub() RaiseEvent StopHotkeyPressed())
    End Sub

    Private Shared Function GetString(element As JsonElement, name As String) As String
        Dim value As JsonElement
        If element.ValueKind = JsonValueKind.Object AndAlso element.TryGetProperty(name, value) Then Return value.ToString()
        Return ""
    End Function

    Private Shared Function GetDouble(element As JsonElement, name As String) As Double
        Dim value As JsonElement
        If element.ValueKind = JsonValueKind.Object AndAlso element.TryGetProperty(name, value) Then
            Dim result As Double
            If value.ValueKind = JsonValueKind.Number AndAlso value.TryGetDouble(result) Then Return result
            If Double.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, result) Then Return result
        End If
        Return 0
    End Function

    Private Shared Function GetInt(element As JsonElement, name As String) As Integer
        Return CInt(GetDouble(element, name))
    End Function

    Public Sub Dispose() Implements IDisposable.Dispose
        If IsDisposed Then Return
        IsDisposed = True
        Hotkeys.Dispose()
        Backend.Dispose()
    End Sub
End Class
