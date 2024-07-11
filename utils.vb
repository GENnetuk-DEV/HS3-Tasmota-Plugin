Imports System.IO
Imports System.Runtime.Serialization.Formatters
Imports HomeSeerAPI

Module utils
    Public IFACE_NAME As String = "TasMQTT"
    Public Const INIFILE As String = "TasMQTT.ini"

    Public callback As HomeSeerAPI.IAppCallbackAPI
    Public hs As HomeSeerAPI.IHSApplication
    Public instance As String = ""
    Public interfaceVersion As Integer
    Public IsShuttingDown As Boolean = False
    Public pluginPath As String = System.IO.Path.GetDirectoryName(System.AppDomain.CurrentDomain.BaseDirectory)
    Public currentPage As Object

    Public ConsoleBufferQ As New Queue(Of ConsoleBuffer)
    Public ConsoleBufferMax As Integer = 0
    Public ConsoleTimer As System.Threading.Timer
    Public ConsoleCharacter As Int16 = 0
    Public ConsoleLogLine As String = ""

    Public DebugQ As Queue = New Queue()
    Public Structure Pair
        Dim name As String
        Dim value As String
    End Structure

    'Public Enum LogType
    '    Debug = -1
    '    Normal = 0
    '    Warning = 1
    '    [Error] = 2
    'End Enum

    Public Class ConsoleBuffer
        Dim _BG As Integer
        Dim _Message As String

        Public Property BG As Integer
            Get
                Return _BG
            End Get
            Set(value As Integer)
                _BG = value
            End Set
        End Property

        Public Property Message
            Get
                Return _Message
            End Get
            Set(value)
                _Message = value
            End Set
        End Property
    End Class

    Public Sub Debug(Level, DebugModule, DebugString)
        Dim BG
        Dim HTMLColor As String = ""
        'Dim ConsoleLine As New ConsoleBuffer

        ' -1 = debug
        ' 0 = normal
        ' 1 = warning
        ' 2 = error
        'If Not IsDBNull(plugin.Settings.DebugLog) Then
        'If plugin.Settings.DebugLog Then
        'hs.WriteLog("rtMQTT", DebugModule.ToString + " : " + DebugString.ToString)
        'End If
        'End If
        Dim LogLine As String = ""
        Select Case Level
            Case 0 : BG = ConsoleColor.Red : HTMLColor = "FF0000"
            Case 1 : BG = ConsoleColor.DarkYellow : HTMLColor = "808000"
            Case 2 : BG = ConsoleColor.Yellow : HTMLColor = "FFFF00"
            Case 3 : BG = ConsoleColor.DarkGreen : HTMLColor = "006400"
            Case 4 : BG = ConsoleColor.DarkCyan : HTMLColor = "008B8B"
            Case 5 : BG = ConsoleColor.Green : HTMLColor = "00FF00"

            Case Else
                BG = ConsoleColor.White
        End Select

        If Level >= GlobalVariables.DebugLevel Then
            Console.ForegroundColor = BG
            LogLine = Now().ToString + ":[" + Level.ToString + "] " + DebugModule.PadLeft(10, " ") + " : " + DebugString.ToString
            Console.WriteLine(LogLine)

            'ConsoleLine.BG = BG
            'ConsoleLine.Message = LogLine
            'ConsoleBufferQ.Enqueue(ConsoleLine)
            'Console.WriteLine(LogLine)
            AddQ("<tr style='font-family: monospace; white-space: nowrap; vertical-align: text-top; background-color: #000000; color:#" + HTMLColor + "'><td style='width:160px;'>" + Now().ToString + "</td><td style='width:20px;'>[" + Level.ToString + "]</td><td style='width:70px;'>" + DebugModule + "</td><td style='white-space: normal; word-wrap: break-word;'>" + DebugString.ToString + "</td></tr>")
            If GlobalVariables.LogFile Then
                If GlobalVariables.RawLog.BaseStream IsNot Nothing Then
                    Try
                        GlobalVariables.RawLog.WriteLine(LogLine)
                        GlobalVariables.RawLog.Flush()
                    Catch ex As Exception
                        Console.WriteLine("EXCEPTION1: " + ex.ToString)
                    End Try
                End If
            End If
            If Level > 4 Then
                Try
                    If hs IsNot Nothing Then hs.WriteLog(IFACE_NAME + "[" + Level.ToString + "]", DebugString.ToString)
                Catch ex As Exception
                    Console.WriteLine("EXCEPTION2: " + ex.ToString)
                End Try
            End If
        End If
    End Sub





    Public Sub AddQ(QData As String)
        DebugQ.Enqueue(QData)
        If DebugQ.Count() > 100 Then DebugQ.Dequeue()
    End Sub

    Public Function BoolToYesNo(ByVal Boolvalue) As String
        If Boolvalue Then Return "Yes" Else Return "No"
    End Function

    Public Sub PEDAdd(ByRef PED As clsPlugExtraData, ByVal PEDName As String, ByVal PEDValue As Object)
        Dim ByteObject() As Byte = Nothing
        If PED Is Nothing Then PED = New clsPlugExtraData
        SerializeObject(PEDValue, ByteObject)

        If Not PED.AddNamed(PEDName, ByteObject) Then 'AddNamed will return False if "PEDName" it already exists
            PED.RemoveNamed(PEDName)
            PED.AddNamed(PEDName, ByteObject)
        End If
    End Sub

    '0 = Info, 1 = Notice, 2 = Warning, 3 = Error, 4 = Critical
    Sub AddWarning(Level As Int16, WarningString As String)
        Dim myWarning = GlobalVariables.Warnings.Find(Function(p) p.Warning = WarningString)
        If myWarning Is Nothing Then
            GlobalVariables.Warnings.Add(New Warnings() With {.TimeStamp = DateTime.Now, .Level = Level, .Warning = WarningString})
        Else
            GlobalVariables.Warnings.Find(Function(p) p.Warning = WarningString).TimeStamp = DateTime.Now
        End If
    End Sub

    Sub ExpireWarning()
        Dim dd As Int16
        Dim ddLimit As Int16
        Dim x As Integer
        If GlobalVariables.Warnings.Count > 0 Then
            For x = 0 To GlobalVariables.Warnings.Count - 1
                Debug(0, "Utils", "X=" + x.ToString + " Count = " + GlobalVariables.Warnings.Count.ToString)
                If x <= GlobalVariables.Warnings.Count - 1 Then
                    dd = DateDiff(DateInterval.Minute, GlobalVariables.Warnings.Item(x).TimeStamp, DateTime.Now)
                    Select Case GlobalVariables.Warnings.Item(x).Level
                        Case 0 : ddLimit = 5
                        Case 1 : ddLimit = 10
                        Case 2 : ddLimit = 15
                        Case 3 : ddLimit = 60
                        Case Else
                            ddLimit = 240
                    End Select
                    Debug(0, "UTILS", "Key [" + GlobalVariables.Warnings.Item(x).TimeStamp.ToString + "]  [" + GlobalVariables.Warnings.Item(x).Warning.ToString + "] DD [" + dd.ToString + "]")
                    If dd > ddLimit Then
                        Debug(4, "UTILS", "Expired Warning " + x.ToString + " [" + GlobalVariables.Warnings.Item(x).Warning.ToString + "]")
                        GlobalVariables.Warnings.RemoveAt(x)
                    End If
                End If
                'If x > GlobalVariables.Warnings.Count - 1 Then Exit For
            Next
        End If
    End Sub

    Function PEDGet(ByRef PED As clsPlugExtraData, ByVal PEDName As String) As Object
        Dim ReturnValue As New Object

        Dim ByteObject() As Byte = PED.GetNamed(PEDName)
        If ByteObject Is Nothing Then Return Nothing

        DeSerializeObject(ByteObject, ReturnValue)
        Return ReturnValue
    End Function

    Public Function SerializeObject(ByRef ObjIn As Object, ByRef bteOut() As Byte) As Boolean
        If ObjIn Is Nothing Then Return False
        Dim memStream As New MemoryStream
        Dim formatter As New Binary.BinaryFormatter

        Try
            formatter.Serialize(memStream, ObjIn)
            ReDim bteOut(CInt(memStream.Length - 1))
            bteOut = memStream.ToArray
            Return True
        Catch ex As Exception
            Debug(9, System.Reflection.MethodBase.GetCurrentMethod().Name, "Serializing object " & ObjIn.ToString & " :" & ex.Message)
            Return False
        End Try
    End Function

    Public Function DeSerializeObject(ByRef bteIn() As Byte, ByRef ObjOut As Object) As Boolean

        'If input and/or output is nothing then it failed (we need some objects to work with), so return False
        If bteIn Is Nothing Then Return False
        If bteIn.Length < 1 Then Return False
        If ObjOut Is Nothing Then Return False

        'Else: Let's deserialize the bytes
        Dim memStream As MemoryStream
        Dim formatter As New Binary.BinaryFormatter
        Dim ObjTest As Object
        Dim TType As System.Type
        Dim OType As System.Type
        Try
            OType = ObjOut.GetType
            ObjOut = Nothing
            memStream = New MemoryStream(bteIn)

            ObjTest = formatter.Deserialize(memStream)
            If ObjTest Is Nothing Then Return False
            TType = ObjTest.GetType
            'If Not TType.Equals(OType) Then Return False

            ObjOut = ObjTest
            If ObjOut Is Nothing Then Return False
            Return True
        Catch exIC As InvalidCastException
            Return False
        Catch ex As Exception
            Debug(9, System.Reflection.MethodBase.GetCurrentMethod().Name, "DeSerializing object: " & ex.Message)
            Return False
        End Try

    End Function

    Function InitDevice(ByVal PName As String, ByVal modNum As Integer, ByVal counter As Integer, Optional ByVal ref As Integer = 0) As Boolean
        Dim dv As Scheduler.Classes.DeviceClass = Nothing
        Debug(9, System.Reflection.MethodBase.GetCurrentMethod().Name, "Initiating Device " & PName)

        Try
            If Not hs.DeviceExistsRef(ref) Then
                ref = hs.NewDeviceRef(PName)
                Try
                    dv = hs.GetDeviceByRef(ref)
                    InitHSDevice(dv, PName)
                    Return True
                Catch ex As Exception
                    Debug(9, System.Reflection.MethodBase.GetCurrentMethod().Name, "Error initializing device " & PName & ": " & ex.Message)
                    Return False
                End Try
            End If
        Catch ex As Exception
            Debug(9, System.Reflection.MethodBase.GetCurrentMethod().Name, "InitDevice: Error getting RefID from deviceCode within InitDevice. (" & ex.Message & ")")
        End Try
        Return False
    End Function

    Sub InitHSDevice(ByRef dv As Scheduler.Classes.DeviceClass, Optional ByVal Name As String = "Optional_Sample_device_name")
        Dim DT As New DeviceTypeInfo
        DT.Device_Type = DeviceTypeInfo.eDeviceAPI.Plug_In
        dv.DeviceType_Set(hs) = DT
        dv.Interface(hs) = plugin.Name
        dv.InterfaceInstance(hs) = instance
        dv.Last_Change(hs) = Now
        dv.Name(hs) = Name
        dv.Location(hs) = plugin.Name
        dv.Device_Type_String(hs) = plugin.Name
        dv.MISC_Set(hs, Enums.dvMISC.SHOW_VALUES)
        dv.MISC_Set(hs, Enums.dvMISC.NO_LOG)
        dv.Status_Support(hs) = False 'Set to True if the devices can be polled, false if not
    End Sub

    Public Sub SendCommand(ByVal houseCode As String, ByVal Devicecode As String)
        'Send a command somewhere, but for now, just log it
        'hs.WriteLog("MoskusSample", "utils.vb -> SendCommand. HouseCode: " & houseCode & " - DeviceCode: " & Devicecode)
    End Sub


    Public Sub RegisterWebPage(ByVal link As String, Optional linktext As String = "", Optional page_title As String = "")
        Try
            Dim the_link As String = link
            hs.RegisterPage(the_link, plugin.Name, instance)

            If linktext = "" Then linktext = link
            linktext = linktext.Replace("_", " ").Replace(plugin.Name, "")
            If page_title = "" Then page_title = linktext

            Dim webPageDescription As New HomeSeerAPI.WebPageDesc
            webPageDescription.plugInName = plugin.Name
            webPageDescription.link = the_link
            webPageDescription.linktext = linktext & instance
            webPageDescription.page_title = page_title & instance

            callback.RegisterLink(webPageDescription)
            Debug(4, "UTILS", "Register Page [" + link + "]")
        Catch ex As Exception
            Debug(9, System.Reflection.MethodBase.GetCurrentMethod().Name, "Registering Web Links (RegisterWebPage): " & ex.Message)
        End Try
    End Sub

    Public Function Devices() As List(Of Scheduler.Classes.DeviceClass)
        Dim ret As New List(Of Scheduler.Classes.DeviceClass)
        Dim device As Scheduler.Classes.DeviceClass
        Dim DE As Scheduler.Classes.clsDeviceEnumeration

        DE = hs.GetDeviceEnumerator()
        Do While Not DE.Finished
            device = DE.GetNext
            ret.Add(device)
        Loop

        Return ret
    End Function

    Public Function Events() As List(Of HomeSeerAPI.strEventData)
        Dim ret As New List(Of HomeSeerAPI.strEventData)

        For Each e As HomeSeerAPI.strEventData In hs.Event_Info_All
            ret.Add(e)
        Next

        Return ret
    End Function

    Function SecstoWords(Secs As Integer)
        If Secs <= 43200 Then SecstoWords = Secs.ToString + " Seconds"
        If Secs > 43200 Then SecstoWords = Math.Floor(Secs / 43200).ToString + " Days"
    End Function

    Public Class Housekeeping
        Public Shared Sub Maintenance()
            Dim RightNow As Date = Now
            Dim LastSeen As Date
            Dim SecondsAgo As Long = 0

            Debug(0, "MAINT", "Maintenance Thread")
            For Each TasDevices As DeviceInfo In GlobalVariables.TasmotaDevices
                LastSeen = TasDevices.LastSeen
                SecondsAgo = DateDiff(DateInterval.Second, LastSeen, RightNow)
                If SecondsAgo >= 120 And TasDevices.Active = True Then
                    TasDevices.Active = False
                    Debug(5, "MAINT", "Device " + TasDevices.DeviceName + " Last Seen " + SecstoWords(SecondsAgo).ToString + " ago, Now INACTIVE")
                    HSPI.SetDevice(TasDevices.DeviceID, "offline")
                ElseIf SecondsAgo < 120 And TasDevices.Active = False Then
                    TasDevices.Active = True
                    Debug(5, "MAINT", "Device " + TasDevices.DeviceName + " Last Seen " + SecstoWords(SecondsAgo).ToString + " ago, Now ACTIVE")
                End If
            Next
        End Sub
    End Class
End Module
