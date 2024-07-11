Imports System.ComponentModel
Imports System.Net
Imports System.Text

Class MQTT

    Public Shared mqttClient As MqttClient
    'Public Shared MQTTSettings As Settings


    'Public Delegate Function Subscription(ByVal Sender As Object, ByVal e As PublishArrivedArgs)

    Public Structure MQ
        Public MTopic As String
        Public MPayload As String
    End Structure
    Public Shared MQTTStruct As New MQ

    Public Shared Sub MQTTPub(Topic As String, Payload As String)
        If mqttClient.IsConnected Then
            GlobalVariables.StatsPublishes = GlobalVariables.StatsPublishes + 1

            Debug(2, "MQTTPub", "Publishing " + Payload + " to " + Topic)
            'mqttClient.Publish(Topic, Payload, 2, False)
            mqttClient.Publish(Topic, Encoding.UTF8.GetBytes(Payload))
        Else
            Debug(2, "MQTT", "Lost Connection to the Broker!")
        End If
    End Sub

    Public Shared Function InitMQTT() As Boolean
        Debug(3, "MQTT", "Attempting to Initialise the MQTT Library")
        Dim Broker As String = ""
        Dim User As String = ""
        Dim Pass As String = ""

        If plugin.Settings.BrokerIPAddress IsNot Nothing Then
            Broker = plugin.Settings.BrokerIPAddress
        Else
            Broker = ""
        End If
        If plugin.Settings.BrokerUsername IsNot Nothing Then
            User = plugin.Settings.BrokerUsername
        Else
            User = ""
        End If
        If plugin.Settings.BrokerPassword IsNot Nothing Then
            Pass = plugin.Settings.BrokerPassword
        Else
            Pass = ""
        End If

        Debug(2, "MQTT", "Broker IP Address [" + Broker + "]")
        Debug(2, "MQTT", "Broker UserName [" + User + "]")
        Debug(2, "MQTT", "Broker Password [" + Pass + "]")

        mqttClient = New MqttClient(Broker)

        Debug(3, "MQTT", "Remove Any existing MQTT Event Handlers")
        Try
            RemoveHandler mqttClient.MqttMsgPublishReceived, AddressOf MQTTMsgArrived
            RemoveHandler mqttClient.ConnectionClosed, AddressOf Client_ConnectionLost
        Catch ex As Exception
            Debug(2, "MQTT", "No Active Event Handlers")
        End Try

        Try
            If User <> "" Then
                Debug(2, "MQTT", "Setup Authenticated Connection")
                mqttClient.Connect(GlobalVariables.ModuleName, User, Pass)

            Else
                Debug(2, "MQTT", "Setup Anonymous Connection")
                mqttClient.Connect(GlobalVariables.ModuleName)
            End If
        Catch
            Debug(2, "MQTT", "Connection Failed")
            AddWarning(3, "MQTT Connection Failed to " + Broker.ToString)
        End Try

        If mqttClient.IsConnected Then
            Debug(3, "MQTT", "Connected to MQTT Broker")
            AddWarning(0, "Connected to MQTT Broker " + Broker.ToString)
            Register(New String() {"stat/#"})
            Register(New String() {"tele/#"})

            Debug(3, "MQTT", "Add Event Handlers")
            AddHandler mqttClient.MqttMsgPublishReceived, AddressOf MQTTMsgArrived
            AddHandler mqttClient.ConnectionClosed, AddressOf Client_ConnectionLost

            Debug(3, "MQTT", "Added Deligate for Subscriptions")
            GlobalVariables.EnableQueue = True
            GlobalVariables.MQTTConnected = True
            Return True
        Else
            Return False
        End If

    End Function

    Public Shared Sub CloseMQTT()
        Debug(5, "MQTT", "Closing down MQTT and threads")
        GlobalVariables.EnableQueue = False
        Threading.Thread.Sleep(5000) ' wait for queue processing to end
        If mqttClient.IsConnected Then mqttClient.Disconnect()
        GlobalVariables.MQTTConnected = False
    End Sub

    Public Shared Sub Client_ConnectionLost()
        Debug(4, "MQTT_Deligate", "Connection Lost")
        GlobalVariables.MQTTConnected = False
        If GlobalVariables.IsShuttingDown = False Then

        End If

    End Sub

    Public Shared Sub Register(Topic As String())
        mqttClient.Subscribe(Topic, New Byte() {Messages.MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE})
        For Each T In Topic
            Debug(4, "MQTT", "Subscribed to [" + T.ToString + "]")
        Next
    End Sub

    Public Shared Function MQTTMsgArrived(Sender, e) As Boolean
        Dim Key As Integer = 0
        Dim Payload As String = Encoding.UTF8.GetString(e.message)
        'Dim DeviceID As Int32 = 0
        GlobalVariables.StatsMessages = GlobalVariables.StatsMessages + 1
        Dim TopicArray As Array = Split(e.topic, "/")
        Dim TopicType As String = TopicArray(0)
        Dim TopicTarget As String = TopicArray(1)
        Dim TopicSubject As String = TopicArray(1) + "/" + TopicArray(2)
        TopicSubject = Trim(TopicSubject)
        Debug(1, "MQTT", "Received Type [" + TopicType + "] Target [" + TopicTarget + "] TopicSubject [" + e.topic.ToString + "] =" + Payload)

        Select Case TopicType
            Case "stat"
                Dim OurDevice As DeviceInfo = GlobalVariables.TasmotaDevices.Find(Function(p) p.DeviceString = TopicSubject)

                If Not OurDevice Is Nothing Then
                    Debug(4, "MQTT", "Found our device changed " + TopicSubject + " in Device " + OurDevice.DeviceID.ToString)
                    If Payload.ToLower = "on" Then Payload = "1" Else Payload = "0"
                    GlobalVariables.MQTTQ.Enqueue("stat|" + OurDevice.DeviceID.ToString + "|" + Payload)
                Else
                    Debug(1, "MQTT", TopicSubject + " is not our device.")
                End If
            Case "tele"
                'Dim OurDevice As New List(Of DeviceInfo)
                'OurDevice = GlobalVariables.TasmotaDevices.Find(Function(p) p.DeviceName = TopicTarget)
                Dim OurDevice = From c In GlobalVariables.TasmotaDevices
                                Where c.DeviceName = TopicTarget
                                Select c

                If Not OurDevice Is Nothing Then
                    For Each TasDevs As DeviceInfo In OurDevice
                        Debug(3, "MQTT", "Found our device Telementry " + TasDevs.DeviceName + " in Device " + TasDevs.DeviceID.ToString)
                        GlobalVariables.MQTTQ.Enqueue("tele|" + TasDevs.DeviceID.ToString + "|" + Payload)
                    Next
                Else
                    Debug(1, "MQTT", "[" + TopicTarget + "] is not our device.")
                End If
            Case Else
                Debug(1, "MQTT", "Unable to handle type " + TopicType)
        End Select

        Return True
    End Function
End Class

Public Class QueueProcessing
    Public Shared Sub ProcessQueue(ByVal State As Object)
        Dim Topic As String = ""
        Dim Payload As String = ""
        Dim Command As String = ""
        Dim dRef As Double = 0
        Dim deQ As String = ""
        Dim IsOn As Boolean = False
        Dim CapiCommand As String = ""
        Dim Target As String = ""
        Dim a, b, c
        Dim TasDeviceName As String = ""
        Dim TasTarget As String = ""
        Dim TasType As UInt16 = 0
        Dim HSDeviceName As String = ""
        Dim Sensor As String = ""
        Dim JSONPair As Array
        Dim JKey As String = ""
        Dim JValue As String = ""
        Dim UpTime As Long = 0
        Dim HoldStatFlag As Boolean = False

        Dim dArray
        If GlobalVariables.EnableQueue Then

            Debug(1, "QPROC", "Queue is Currently " + GlobalVariables.MQTTQ.Count().ToString)
            While GlobalVariables.MQTTQ.Count() > 0
                GlobalVariables.StatsqProcessed = GlobalVariables.StatsqProcessed + 1
                deQ = GlobalVariables.MQTTQ.Dequeue
                dArray = Split(deQ, "|")
                If UBound(dArray) > 0 Then
                    Command = dArray(0).ToString
                    Topic = dArray(1).ToString
                    Payload = dArray(2).ToString

                    Debug(2, "QPROC", "Command [" + Command + "] Topic [" + Topic + "] PayLoad [" + Payload + "]")
                    Select Case Command
                        Case "cmnd"   'topic is the MQTT path
                            GlobalVariables.TasmotaDevices.Find(Function(p) p.DeviceString = Topic).IgnoreTele = True
                            MQTT.MQTTPub(Command + "/" + Topic, Payload)
                            'hs.WriteLog(GlobalVariables.ModuleName, "Published " + Payload + " to " + Topic)
                            Debug(5, "QPROC", "HS: Setting " + Topic + " to " + Payload.ToString)
                        Case "stat"   'topic is the device ref
                            If IsNumeric(Topic) Then
                                dRef = CDbl(Topic)
                                IsOn = hs.IsON(dRef)
                                If Payload = "0" Then CapiCommand = "off" Else CapiCommand = "on"
                                Debug(2, "QPROC", "HS Device " + dRef.ToString + " = " + IsOn.ToString)
                                GlobalVariables.TasmotaDevices.Find(Function(p) p.DeviceID = CInt(Topic)).LastSeen = Now 'its just seen because its is. 
                                If GlobalVariables.TasmotaDevices.Find(Function(p) p.DeviceID = CInt(Topic)).Pending = False Then
                                    If (IsOn And Payload = "0") Or (Not IsOn And Payload = "1") Then
                                        Debug(5, "QPROC", "Updating Device " + dRef.ToString + " = " + CapiCommand)
                                        HSPI.SetDevice(dRef, CapiCommand)
                                    Else
                                        Debug(4, "QPROC", "No Change needed in HS")
                                    End If
                                Else
                                    Debug(5, "QPROC", "Skipping Update Device " + dRef.ToString + " cmnd Pending ")
                                    GlobalVariables.TasmotaDevices.Find(Function(p) p.DeviceID = CInt(Topic)).Pending = False
                                End If
                            Else
                                    Debug(3, "QPROC", "Topic is not numeric for stat command")
                            End If
                        Case "TelePeriod"
                            MQTT.MQTTPub("cmnd/" + Topic + "/" + Command, Payload)
                        Case "tele"
                            'Topic is our DeviceID
                            If IsNumeric(Topic) Then
                                GlobalVariables.TasmotaDevices.Find(Function(p) p.DeviceID = CInt(Topic)).LastSeen = Now
                                TasDeviceName = GlobalVariables.TasmotaDevices.Find(Function(p) p.DeviceID = CInt(Topic)).DeviceName.ToString
                                HSDeviceName = GlobalVariables.TasmotaDevices.Find(Function(p) p.DeviceID = CInt(Topic)).HSDeviceName.ToString
                                TasTarget = GlobalVariables.TasmotaDevices.Find(Function(p) p.DeviceID = CInt(Topic)).DeviceTarget.ToString
                                TasType = GlobalVariables.TasmotaDevices.Find(Function(p) p.DeviceID = CInt(Topic)).Type
                                Debug(2, "QPROC", "DeviceID " + Topic + " " + TasDeviceName + " Processing Telemetry Payload")
                                Select Case Payload
                                    Case "Offline"
                                        GlobalVariables.TasmotaDevices.Find(Function(p) p.DeviceID = CInt(Topic)).Active = False
                                        Debug(5, "QPROC", "Device " + HSDeviceName + " (" + Topic.ToString + ":" + TasDeviceName + ") has gone offline, updating HS")
                                        AddWarning(2, "Device " + HSDeviceName + " has gone offline")
                                        HSPI.SetDevice(CInt(Topic), "offline")
                                    Case "Online"
                                        GlobalVariables.TasmotaDevices.Find(Function(p) p.DeviceID = CInt(Topic)).Active = True
                                        Debug(5, "QPROC", "Device " + HSDeviceName + " (" + Topic.ToString + ":" + TasDeviceName + ") is back online.")
                                        AddWarning(1, "Device " + HSDeviceName + " is back online")
                                        GlobalVariables.MQTTQ.Enqueue("TelePeriod|" + TasDeviceName + "|60")
                                    Case Else
                                        Dim json As Array = Split(Replace(Payload, Chr(34), ""), ",")
                                        For Each J In json
                                            J = Replace(J, "{", "")
                                            J = Replace(J, "}", "")
                                            J = Trim(J)
                                            JSONPair = Split(J, ":")   'Split json value pairs
                                            Select Case UBound(JSONPair)
                                                Case 0 : JKey = JSONPair(0) : JValue = ""
                                                Case 1 : JKey = JSONPair(0) : JValue = JSONPair(1)
                                                Case 2 : JKey = JSONPair(1) : JValue = JSONPair(2)
                                                Case Else
                                                    Debug(1, "QPROC", "Invalid JSON Packet [" + J + "]")
                                                    JKey = "" : JValue = ""
                                            End Select
                                            Debug(1, "QPROC", "J=[" + J.ToString + "] Key[" + JKey + "] Value[" + JValue + "]")
                                            If InStr(JKey, "POWER") > 0 Then
                                                If GlobalVariables.TasmotaDevices.Find(Function(p) p.DeviceID = CInt(Topic)).DeviceTarget = JKey Then
                                                    If GlobalVariables.TasmotaDevices.Find(Function(p) p.DeviceID = CInt(Topic)).IgnoreTele = False Then
                                                        IsOn = hs.IsON(CDbl(Topic))
                                                        If IsOn And JValue = "OFF" Or Not IsOn And JValue = "ON" Then
                                                            Debug(5, "QPROC", "Telementry Indicates Device " + HSDeviceName + " (" + Topic.ToString + ":" + TasDeviceName + ") is currently " + JValue + " which is different to HS - Updating HS")
                                                            AddWarning(1, "Variance Detected between " + HSDeviceName + " and " + TasDeviceName + " - " + Topic.ToString + " Correcting")
                                                            'shit - its not the same
                                                            HSPI.SetDevice(CDbl(Topic), JValue)
                                                        Else
                                                            Debug(4, "QPROC", "Telementry Indicates Device " + HSDeviceName + " (" + Topic.ToString + ":" + TasDeviceName + ") is currently " + JValue + " which is the same as HS - Ignoring")
                                                        End If
                                                    Else
                                                        Debug(5, "QPROC", "Telementry Indicates Device " + HSDeviceName + " (" + Topic.ToString + ":" + TasDeviceName + ") is currently " + JValue + " which is different to HS - BUT We're told to ignore this")
                                                        GlobalVariables.TasmotaDevices.Find(Function(p) p.DeviceID = CInt(Topic)).IgnoreTele = False
                                                    End If

                                                End If

                                                End If
                                            If JKey = "RSSI" Then
                                                If IsNumeric(JValue) Then
                                                    Debug(4, "QPROC", "Device " + HSDeviceName + " RSSI = " + JValue)
                                                    GlobalVariables.TasmotaDevices.Find(Function(p) p.DeviceID = CInt(Topic)).RSSI = CInt(JValue)
                                                    If CInt(JValue) < 30 Then
                                                        Debug(5, "QPROC", "Device " + HSDeviceName + " (" + Topic.ToString + ":" + TasDeviceName + " Low Wifi Signal Strength")
                                                        AddWarning(2, "Device " + HSDeviceName + " (" + Topic.ToString + ":" + TasDeviceName + " Low Wifi Signal Strength")
                                                    End If
                                                End If
                                            End If
                                            If JKey.ToUpper = "UPTIME" Then
                                                If IsNumeric(JValue) Then
                                                    Debug(4, "QPROC", "Device " + HSDeviceName + " UpTime = " + JValue)
                                                    UpTime = GlobalVariables.TasmotaDevices.Find(Function(p) p.DeviceID = CInt(Topic)).Uptime
                                                    If CLng(JValue) < UpTime Then
                                                        AddWarning(2, "Device " + HSDeviceName + " Seems to have rebooted")
                                                    End If
                                                    GlobalVariables.TasmotaDevices.Find(Function(p) p.DeviceID = CInt(Topic)).Uptime = CLng(JValue)
                                                End If
                                            End If
                                            If TasType = 1 Then
                                                If TasTarget = JKey Then
                                                    Debug(4, "QPROC", "Found Sensor Value Pair [" + JKey + "] = [" + JValue + "] for HS Device " + Topic.ToString)
                                                    HSPI.SetDeviceValue(CDbl(Topic), CDbl(JValue))
                                                End If
                                            End If
                                        Next
                                End Select
                            Else
                                Debug(2, "QPROC", "Topic is not numeric for tele command")
                            End If
                        Case Else
                            Debug(2, "QPROC", "No idea how to process " + Command)
                    End Select
                Else
                    Debug(1, "QPROC", "Invalid Split")
                End If

            End While
        Else
            Debug(4, "QPROC", "Queue Processing not active")
        End If

    End Sub

End Class
