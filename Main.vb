Imports Scheduler
Imports HomeSeerAPI
Imports HSCF.Communication.Scs.Communication.EndPoints.Tcp
Imports HSCF.Communication.ScsServices.Client
Imports HSCF.Communication.ScsServices.Service
Imports System.Reflection

Module Main

    Public Version As String = ""
    Public Release As String = "Public Beta"

    Public assemblyVersion As String = Assembly.GetExecutingAssembly().GetName().Version.ToString()

    Public WithEvents client As HSCF.Communication.ScsServices.Client.IScsServiceClient(Of IHSApplication)
    Dim WithEvents clientCallback As HSCF.Communication.ScsServices.Client.IScsServiceClient(Of IAppCallbackAPI)
    Dim ReleaseTimer As New System.Timers.Timer
    Dim StatsTimer As New System.Timers.Timer
    Dim MQTTTimer As New System.Timers.Timer

    Public Statistics As String = "No Statistics Yet"

    Private host As HomeSeerAPI.IHSApplication
    Private appAPI As HSPI
    Public plugin As New CurrentPlugin  ' real plugin functions, user supplied

    Sub Main()
        Dim ip As String = "127.0.0.1" 'Default is connecting to the local server

        Dim Varr = Split(assemblyVersion, ".")
        Version = Varr(0) + "."
        For i = 1 To UBound(Varr)
            Version = Version + Varr(i)
        Next

        Debug(5, "Main", "Starting Plug-in Version " + Version.ToString)

        'Let's check the startup arguments. Here you can set the server location (IP) if you are running the plugin remotely, and set an optional instance name
        For Each argument As String In My.Application.CommandLineArgs
            Dim parts() As String = argument.Split("=")
            Select Case parts(0).ToLower
                Case "server" : ip = parts(1)
                Case "instance"
                    Try
                        instance = parts(1)
                    Catch ex As Exception
                        instance = ""
                    End Try
            End Select
        Next

        appAPI = New HSPI

        Debug(5, "Main", "Connecting to server at " & ip & "...")
        client = ScsServiceClientBuilder.CreateClient(Of IHSApplication)(New ScsTcpEndPoint(ip, 10400), appAPI)
        clientCallback = ScsServiceClientBuilder.CreateClient(Of IAppCallbackAPI)(New ScsTcpEndPoint(ip, 10400), appAPI)

        ReleaseTimer.AutoReset = True
        ReleaseTimer.Interval = 8640000
        AddHandler ReleaseTimer.Elapsed, AddressOf GetRelease
        'GetRelease()
        ReleaseTimer.Start()

        StatsTimer.AutoReset = True
        StatsTimer.Interval = 60000
        AddHandler StatsTimer.Elapsed, AddressOf MakeStats
        StatsTimer.Start()

        MQTTTimer.AutoReset = True
        MQTTTimer.Interval = 30000
        AddHandler MQTTTimer.Elapsed, AddressOf ConnectMQTT

        Dim Attempts As Integer = 1

TryAgain:
        Try
            client.Connect()
            clientCallback.Connect()

            host = client.ServiceProxy
            Dim APIVersion As Double = host.APIVersion  ' will cause an error if not really connected

            callback = clientCallback.ServiceProxy
            APIVersion = callback.APIVersion  ' will cause an error if not really connected
            Debug(5, "Main", "Connected to Version " + APIVersion.ToString)

        Catch ex As Exception
            Debug(5, "Main", "Cannot connect attempt " & Attempts.ToString & ": " & ex.Message)
            If ex.Message.ToLower.Contains("timeout occurred.") Then
                Attempts += 1
                If Attempts < 6 Then GoTo TryAgain
            End If

            If client IsNot Nothing Then
                client.Dispose()
                client = Nothing
            End If
            If clientCallback IsNot Nothing Then
                clientCallback.Dispose()
                clientCallback = Nothing
            End If
            SleepSeconds(4)
            Return
        End Try



        Try
            callback = callback
            hs = host
            ' connect to HS so it can register a callback to us
            MQTTTimer.Start()
            Debug(4, "Main", "Enabled MQTT Supervision")
            AddWarning(0, "Enabled MQTT Supervision Threads")

            host.Connect(plugin.Name, "")
            Debug(5, "Main", "Connected, waiting to be initialised")
            AddWarning(0, "Plug-in Connected")
            Do
                Threading.Thread.Sleep(30)
            Loop While client.CommunicationState = HSCF.Communication.Scs.Communication.CommunicationStates.Connected And Not IsShuttingDown
            If Not IsShuttingDown Then
                StatsTimer.Stop()
                ReleaseTimer.Stop()
                MQTTTimer.Stop()
                plugin.ShutdownIO()
                Debug(5, "Main", "Connection lost, exiting")
            Else
                Debug(5, "Main", "Shutting down plugin")
            End If


            ' disconnect from server for good here
            client.Disconnect()
            clientCallback.Disconnect()
            SleepSeconds(2)
            End
        Catch ex As Exception
            Debug(5, "Main", "Cannot connect(2): " & ex.Message)
            SleepSeconds(2)
            End
            Return
        End Try


    End Sub

    Public Sub GetRelease()
            GlobalVariables.ReleaseNotes = "GPL Release"
    End Sub

    Public Sub MakeStats()
        'Make General Statistics
        Dim Stats As String = ""
        Debug(3, "Main", "Gather Performance Statistics")
        Stats = "<table width='100%'><tr><td>Queue Events</td><td>" + GlobalVariables.StatsqProcessed.ToString + "/min</td></tr>"
        Stats = Stats + "<tr><td>HS3 Events</td><td>" + GlobalVariables.StatsHSEvents.ToString + "/min</td></tr>"
        Stats = Stats + "<tr><td>MQTT Subs</td><td>" + GlobalVariables.StatsMessages.ToString + "/min</td></tr>"
        Stats = Stats + "<tr><td>MQTT Pubs</td><td>" + GlobalVariables.StatsPublishes.ToString + "/min</td></tr></table>"

        GlobalVariables.StatsqProcessed = 0
        GlobalVariables.StatsHSEvents = 0
        GlobalVariables.StatsMessages = 0
        GlobalVariables.StatsPublishes = 0
        Statistics = Stats
        ExpireWarning()
    End Sub

    Public Sub ConnectMQTT()
        If Not GlobalVariables.MQTTConnected And Not GlobalVariables.IsShuttingDown Then
            If MQTT.InitMQTT Then
                Debug(5, "Main", "MQTT Reports Ready")
            Else
                Debug(5, "Main", "MQTT Reports an issue")
            End If
        Else
            Debug(4, "Main", "MQTT Connected")
        End If
    End Sub

    Private Sub Client_Disconnected(ByVal sender As Object, ByVal e As System.EventArgs) Handles client.Disconnected
        Debug(5, "Main", "Disconnected from server - client")
        AddWarning(1, "MQTT Session Disconnected")
    End Sub

    Private Sub SleepSeconds(ByVal secs As Integer)
        Threading.Thread.Sleep(secs * 1000)
    End Sub
End Module
