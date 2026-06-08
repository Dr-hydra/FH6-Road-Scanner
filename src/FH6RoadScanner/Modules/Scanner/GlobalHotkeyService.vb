Imports System.Runtime.InteropServices
Imports System.Windows.Threading

Public Class GlobalHotkeyService
    Implements IDisposable

    Private ReadOnly PollTimer As New DispatcherTimer With {
        .Interval = TimeSpan.FromMilliseconds(50)
    }
    Private StartVirtualKey As Integer
    Private StopVirtualKey As Integer
    Private StartWasDown As Boolean
    Private StopWasDown As Boolean
    Private IsDisposed As Boolean

    Public Event StartPressed()
    Public Event StopPressed()

    Public Sub New()
        AddHandler PollTimer.Tick, AddressOf PollTimer_Tick
    End Sub

    Public Sub Configure(startHotkey As String, stopHotkey As String)
        StartVirtualKey = ToVirtualKey(startHotkey)
        StopVirtualKey = ToVirtualKey(stopHotkey)
        StartWasDown = IsDown(StartVirtualKey)
        StopWasDown = IsDown(StopVirtualKey)
    End Sub

    Public Sub Start()
        If Not IsDisposed Then PollTimer.Start()
    End Sub

    Private Sub PollTimer_Tick(sender As Object, e As EventArgs)
        Dim startIsDown = IsDown(StartVirtualKey)
        Dim stopIsDown = IsDown(StopVirtualKey)

        If startIsDown AndAlso Not StartWasDown Then RaiseEvent StartPressed()
        If stopIsDown AndAlso Not StopWasDown Then RaiseEvent StopPressed()

        StartWasDown = startIsDown
        StopWasDown = stopIsDown
    End Sub

    Public Shared Function IsSupported(value As String) As Boolean
        Return ToVirtualKey(value) <> 0
    End Function

    Private Shared Function ToVirtualKey(value As String) As Integer
        If String.IsNullOrWhiteSpace(value) Then Return 0
        Dim normalized = value.Trim().ToUpperInvariant()
        If Not normalized.StartsWith("F", StringComparison.Ordinal) Then Return 0

        Dim number As Integer
        If Not Integer.TryParse(normalized.Substring(1), number) OrElse number < 1 OrElse number > 12 Then Return 0
        Return &H70 + number - 1
    End Function

    Private Shared Function IsDown(virtualKey As Integer) As Boolean
        Return virtualKey <> 0 AndAlso (GetAsyncKeyState(virtualKey) And &H8000S) <> 0
    End Function

    Public Sub Dispose() Implements IDisposable.Dispose
        If IsDisposed Then Return
        IsDisposed = True
        PollTimer.Stop()
        RemoveHandler PollTimer.Tick, AddressOf PollTimer_Tick
    End Sub

    <DllImport("user32.dll")>
    Private Shared Function GetAsyncKeyState(virtualKey As Integer) As Short
    End Function
End Class
