Imports System.Runtime.InteropServices
Imports System.Windows.Interop
Imports System.Windows.Threading

Public Class StatusOverlayWindow

    Private Const GwlExStyle As Integer = -20
    Private Const WsExToolWindow As Integer = &H80
    Private Const WsExNoActivate As Integer = &H8000000

    Private ReadOnly Controller As ScannerAppController
    Private ReadOnly ResetTimer As New DispatcherTimer With {
        .Interval = TimeSpan.FromSeconds(2.5)
    }

    Public Sub New(controller As ScannerAppController)
        Me.Controller = controller
        InitializeComponent()

        AddHandler controller.StatusChanged, AddressOf OnStatusChanged
        AddHandler controller.ProgressChanged, AddressOf OnProgressChanged
        AddHandler controller.ScanStateChanged, AddressOf OnScanStateChanged
        AddHandler controller.BackendAvailabilityChanged, AddressOf OnBackendAvailabilityChanged
        AddHandler controller.ConfigurationChanged, AddressOf OnConfigurationChanged
        AddHandler ResetTimer.Tick, AddressOf ResetTimer_Tick
        UpdateHotkeyText(controller.Configuration)
    End Sub

    Private Sub StatusOverlayWindow_SourceInitialized(sender As Object, e As EventArgs) Handles Me.SourceInitialized
        Dim handle = New WindowInteropHelper(Me).Handle
        Dim styles = GetWindowLongPtr(handle, GwlExStyle).ToInt64()
        SetWindowLongPtr(handle, GwlExStyle, New IntPtr(styles Or WsExToolWindow Or WsExNoActivate))
    End Sub

    Private Sub StatusOverlayWindow_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        PlaceAtTopRight()
    End Sub

    Private Sub PanOverlay_MouseLeftButtonDown(sender As Object, e As MouseButtonEventArgs) Handles PanOverlay.MouseLeftButtonDown
        If e.ButtonState = MouseButtonState.Pressed Then DragMove()
    End Sub

    Private Sub OnStatusChanged(text As String)
        LabOverlayStatus.Text = text
    End Sub

    Private Sub OnProgressChanged(value As Double)
        OverlayProgress.Value = Math.Max(0, Math.Min(100, value))
        LabOverlayProgress.Text = $"{OverlayProgress.Value:0}%"
    End Sub

    Private Sub OnScanStateChanged(isRunning As Boolean, stateText As String)
        ResetTimer.Stop()
        LabOverlayState.Text = stateText

        If isRunning Then
            StateDot.Fill = New SolidColorBrush(If(stateText = "正在停止", Color.FromRgb(&HCE, &H21, &H11), Color.FromRgb(&H13, &H70, &HF3)))
            If stateText = "扫描中" Then
                OverlayProgress.Value = 0
                LabOverlayProgress.Text = "0%"
                LabOverlayStatus.Text = $"请勿移动鼠标，按 {Controller.Configuration.StopHotkey} 可停止扫描"
            End If
        Else
            StateDot.Fill = New SolidColorBrush(
                If(stateText = "扫描出错", Color.FromRgb(&HCE, &H21, &H11), Color.FromRgb(&H22, &HA0, &H6B)))
            LabOverlayStatus.Text = $"{stateText}，按 {Controller.Configuration.StartHotkey} 可重新开始"
            ResetTimer.Start()
        End If
        ShowWithoutActivation()
    End Sub

    Private Sub OnBackendAvailabilityChanged(available As Boolean)
        If Controller.IsScanning Then Return
        SetReadyState(available)
    End Sub

    Private Sub OnConfigurationChanged(config As ScannerConfiguration)
        UpdateHotkeyText(config)
        If Not Controller.IsScanning Then SetReadyState(Controller.IsBackendAvailable)
    End Sub

    Public Sub ShowAtStartup()
        UpdateHotkeyText(Controller.Configuration)
        SetReadyState(Controller.IsBackendAvailable)
        ShowWithoutActivation()
    End Sub

    Private Sub UpdateHotkeyText(config As ScannerConfiguration)
        LabOverlayHotkeys.Text = $"{config.StartHotkey} 开始 / {config.StopHotkey} 停止"
    End Sub

    Private Sub SetReadyState(backendAvailable As Boolean)
        ResetTimer.Stop()
        OverlayProgress.Value = 0
        LabOverlayProgress.Text = "0%"
        If backendAvailable Then
            LabOverlayState.Text = "就绪"
            LabOverlayStatus.Text = $"按 {Controller.Configuration.StartHotkey} 开始扫描，按 {Controller.Configuration.StopHotkey} 停止扫描"
            StateDot.Fill = New SolidColorBrush(Color.FromRgb(&H22, &HA0, &H6B))
        Else
            LabOverlayState.Text = "连接中"
            LabOverlayStatus.Text = "正在连接扫描后端……"
            StateDot.Fill = New SolidColorBrush(Color.FromRgb(&H13, &H70, &HF3))
        End If
        ShowWithoutActivation()
    End Sub

    Private Sub ShowWithoutActivation()
        If Not IsVisible Then
            PlaceAtTopRight()
            Show()
        End If
        Topmost = True
    End Sub

    Private Sub PlaceAtTopRight()
        Dim area = SystemParameters.WorkArea
        Left = area.Right - Width - 18
        Top = area.Top + 18
    End Sub

    Private Sub ResetTimer_Tick(sender As Object, e As EventArgs)
        ResetTimer.Stop()
        SetReadyState(Controller.IsBackendAvailable)
    End Sub

    Public Sub Detach()
        ResetTimer.Stop()
        RemoveHandler Controller.StatusChanged, AddressOf OnStatusChanged
        RemoveHandler Controller.ProgressChanged, AddressOf OnProgressChanged
        RemoveHandler Controller.ScanStateChanged, AddressOf OnScanStateChanged
        RemoveHandler Controller.BackendAvailabilityChanged, AddressOf OnBackendAvailabilityChanged
        RemoveHandler Controller.ConfigurationChanged, AddressOf OnConfigurationChanged
        Close()
    End Sub

    <DllImport("user32.dll", EntryPoint:="GetWindowLongPtrW")>
    Private Shared Function GetWindowLongPtr64(windowHandle As IntPtr, index As Integer) As IntPtr
    End Function

    <DllImport("user32.dll", EntryPoint:="GetWindowLongW")>
    Private Shared Function GetWindowLong32(windowHandle As IntPtr, index As Integer) As Integer
    End Function

    Private Shared Function GetWindowLongPtr(windowHandle As IntPtr, index As Integer) As IntPtr
        If IntPtr.Size = 8 Then Return GetWindowLongPtr64(windowHandle, index)
        Return New IntPtr(GetWindowLong32(windowHandle, index))
    End Function

    <DllImport("user32.dll", EntryPoint:="SetWindowLongPtrW")>
    Private Shared Function SetWindowLongPtr64(windowHandle As IntPtr, index As Integer, newStyle As IntPtr) As IntPtr
    End Function

    <DllImport("user32.dll", EntryPoint:="SetWindowLongW")>
    Private Shared Function SetWindowLong32(windowHandle As IntPtr, index As Integer, newStyle As Integer) As Integer
    End Function

    Private Shared Function SetWindowLongPtr(windowHandle As IntPtr, index As Integer, newStyle As IntPtr) As IntPtr
        If IntPtr.Size = 8 Then Return SetWindowLongPtr64(windowHandle, index, newStyle)
        Return New IntPtr(SetWindowLong32(windowHandle, index, newStyle.ToInt32()))
    End Function

End Class
