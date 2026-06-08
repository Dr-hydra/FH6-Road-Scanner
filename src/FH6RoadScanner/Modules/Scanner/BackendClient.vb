Imports System.Collections.Concurrent
Imports System.Diagnostics
Imports System.Text
Imports System.Text.Json

Public Class BackendClient
    Implements IDisposable

    Private ReadOnly Pending As New ConcurrentDictionary(Of String, TaskCompletionSource(Of JsonElement))()
    Private ReadOnly WriteLock As New SemaphoreSlim(1, 1)
    Private BackendProcess As Process
    Private IsDisposed As Boolean
    Private HandshakeComplete As Boolean

    Public Event EventReceived(message As JsonElement)
    Public Event DiagnosticReceived(text As String)
    Public Event ConnectionChanged(connected As Boolean)

    Public ReadOnly Property IsConnected As Boolean
        Get
            Return IsProcessRunning AndAlso HandshakeComplete
        End Get
    End Property

    Private ReadOnly Property IsProcessRunning As Boolean
        Get
            Return BackendProcess IsNot Nothing AndAlso Not BackendProcess.HasExited
        End Get
    End Property

    Public Async Function StartAsync() As Task
        If IsConnected Then Return
        HandshakeComplete = False

        Dim root = FindRepositoryRoot()
        Dim packagedBackend = IO.Path.Combine(PathExeFolder, "FH6ScannerBackend.exe")
        Dim startInfo As New ProcessStartInfo With {
            .UseShellExecute = False,
            .CreateNoWindow = True,
            .RedirectStandardInput = True,
            .RedirectStandardOutput = True,
            .RedirectStandardError = True,
            .WorkingDirectory = If(root, PathExeFolder),
            .StandardInputEncoding = New UTF8Encoding(False),
            .StandardOutputEncoding = Encoding.UTF8,
            .StandardErrorEncoding = Encoding.UTF8
        }
        startInfo.Environment("PYTHONUTF8") = "1"
        startInfo.Environment("PYTHONIOENCODING") = "utf-8"

        If IO.File.Exists(packagedBackend) Then
            startInfo.FileName = packagedBackend
            startInfo.WorkingDirectory = PathExeFolder
        Else
            startInfo.FileName = "python"
            startInfo.ArgumentList.Add("-u")
            startInfo.ArgumentList.Add("-m")
            startInfo.ArgumentList.Add("fh6_scanner.backend")
        End If

        BackendProcess = New Process With {.StartInfo = startInfo, .EnableRaisingEvents = True}
        AddHandler BackendProcess.Exited, AddressOf ProcessExited
        If Not BackendProcess.Start() Then Throw New InvalidOperationException("无法启动扫描后端。")

        Dim outputReader = Task.Run(AddressOf ReadOutputLoopAsync)
        Dim errorReader = Task.Run(AddressOf ReadErrorLoopAsync)
        Try
            Await SendCommandAsync("ping").WaitAsync(TimeSpan.FromSeconds(30))
        Catch
            If BackendProcess IsNot Nothing AndAlso Not BackendProcess.HasExited Then BackendProcess.Kill(True)
            Throw New InvalidOperationException("扫描后端启动失败或未通过连接检查。")
        End Try
        HandshakeComplete = True
        RaiseEvent ConnectionChanged(True)
    End Function

    Public Async Function SendCommandAsync(command As String, Optional args As Object = Nothing) As Task(Of JsonElement)
        If Not IsProcessRunning Then Throw New InvalidOperationException("扫描后端未连接。")

        Dim requestId = Guid.NewGuid().ToString("N")
        Dim completion = New TaskCompletionSource(Of JsonElement)(TaskCreationOptions.RunContinuationsAsynchronously)
        If Not Pending.TryAdd(requestId, completion) Then Throw New InvalidOperationException("无法登记后端请求。")

        Dim envelope As New Dictionary(Of String, Object) From {
            {"type", "command"},
            {"id", requestId},
            {"command", command},
            {"args", If(args, New Dictionary(Of String, Object)())}
        }
        Dim line = JsonSerializer.Serialize(envelope)

        Await WriteLock.WaitAsync()
        Try
            Await BackendProcess.StandardInput.WriteLineAsync(line)
            Await BackendProcess.StandardInput.FlushAsync()
        Catch
            Pending.TryRemove(requestId, Nothing)
            Throw
        Finally
            WriteLock.Release()
        End Try

        Dim response = Await completion.Task.WaitAsync(TimeSpan.FromSeconds(15))
        Dim okValue As JsonElement
        If Not response.TryGetProperty("ok", okValue) OrElse Not okValue.GetBoolean() Then
            Dim errorValue As JsonElement
            Dim message = If(response.TryGetProperty("error", errorValue), errorValue.GetString(), "后端请求失败。")
            Throw New InvalidOperationException(message)
        End If

        Dim result As JsonElement
        If response.TryGetProperty("result", result) Then Return result.Clone()
        Return Nothing
    End Function

    Public Async Function ShutdownAsync() As Task
        If Not IsProcessRunning Then Return
        Try
            Await SendCommandAsync("stop_scan").WaitAsync(TimeSpan.FromSeconds(1))
        Catch
        End Try
        Try
            Await SendCommandAsync("shutdown").WaitAsync(TimeSpan.FromSeconds(1))
        Catch
        End Try

        If BackendProcess IsNot Nothing AndAlso Not BackendProcess.HasExited Then
            Try
                Await BackendProcess.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(2))
            Catch
                BackendProcess.Kill(True)
            End Try
        End If
    End Function

    Private Async Function ReadOutputLoopAsync() As Task
        Try
            Do While BackendProcess IsNot Nothing
                Dim line = Await BackendProcess.StandardOutput.ReadLineAsync()
                If line Is Nothing Then Exit Do
                If String.IsNullOrWhiteSpace(line) Then Continue Do
                Using document = JsonDocument.Parse(line)
                    Dim message = document.RootElement.Clone()
                    Dim typeValue As JsonElement
                    If message.TryGetProperty("type", typeValue) AndAlso typeValue.GetString() = "response" Then
                        Dim idValue As JsonElement
                        If message.TryGetProperty("id", idValue) Then
                            Dim completion As TaskCompletionSource(Of JsonElement) = Nothing
                            If Pending.TryRemove(idValue.GetString(), completion) Then completion.TrySetResult(message)
                        End If
                    Else
                        RaiseEvent EventReceived(message)
                    End If
                End Using
            Loop
        Catch ex As Exception
            If Not IsDisposed Then RaiseEvent DiagnosticReceived("读取后端输出失败：" & ex.Message)
        End Try
    End Function

    Private Async Function ReadErrorLoopAsync() As Task
        Try
            Do While BackendProcess IsNot Nothing
                Dim line = Await BackendProcess.StandardError.ReadLineAsync()
                If line Is Nothing Then Exit Do
                If Not String.IsNullOrWhiteSpace(line) Then RaiseEvent DiagnosticReceived(line)
            Loop
        Catch
        End Try
    End Function

    Private Sub ProcessExited(sender As Object, e As EventArgs)
        HandshakeComplete = False
        For Each item In Pending
            item.Value.TrySetException(New InvalidOperationException("扫描后端已退出。"))
        Next
        Pending.Clear()
        RaiseEvent ConnectionChanged(False)
    End Sub

    Private Shared Function FindRepositoryRoot() As String
        Dim candidates = {Environment.CurrentDirectory, PathExeFolder}
        For Each candidate In candidates
            Dim directory = New IO.DirectoryInfo(candidate)
            For i = 0 To 8
                If directory Is Nothing Then Exit For
                If IO.Directory.Exists(IO.Path.Combine(directory.FullName, "fh6_scanner")) Then Return directory.FullName
                directory = directory.Parent
            Next
        Next
        Return PathExeFolder
    End Function

    Public Sub Dispose() Implements IDisposable.Dispose
        If IsDisposed Then Return
        IsDisposed = True
        WriteLock.Dispose()
        If BackendProcess IsNot Nothing Then BackendProcess.Dispose()
    End Sub
End Class
