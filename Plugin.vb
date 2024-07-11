Imports System.Text
Imports System.IO
Imports System.Threading
Imports System.Web
Imports System
Imports HomeSeerAPI
Imports Scheduler
Imports System.Collections.Specialized

Public Class CurrentPlugin
    Implements IPlugInAPI
    Public Settings As Settings 'My simple class for reading and writing settings
    Dim WithEvents updateTimer As Threading.Timer
    Private lastRandomNumber As Integer 'The last random value

    Dim configPageName As String = Me.Name & "Config"
    Dim supportPageName As String = Me.Name & "Status"
    Dim configPage As New web_config(configPageName)
    Dim SupportPage As New web_support(supportPageName)
    Dim webPage As Object

    Dim actions As New hsCollection
    Dim triggers As New hsCollection
    Dim trigger As New Trigger
    Dim action As New Action

    Private Shared myTimer1 As System.Threading.Timer
    Private Shared MyTimer2 As System.Threading.Timer



    Const Pagename = "Events" 'The controls we build here (in Plugin.vb, typically for controlling actions, conditions and triggers) are all located on the Events page


#Region "Init"


    ''' <summary>
    ''' If your plugin is set to start when HomeSeer starts, or is enabled from the interfaces page, then this function will be called to initialize your plugin. If you returned TRUE from HSComPort then the port number as configured in HomeSeer will be passed to this function. Here you should initialize your plugin fully. The hs object is available to you to call the HomeSeer scripting API as well as the callback object so you can call into the HomeSeer plugin API.  HomeSeer's startup routine waits for this function to return a result, so it is important that you try to exit this procedure quickly.  If your hardware has a long initialization process, you can check the configuration here and if everything is set up correctly, start a separate thread to initialize the hardware and exit this sub.  If you encounter an error, you can always use InterfaceStatus to indicate this.
    ''' </summary>
    ''' <param name="port">Optional COM port passed by HomeSeer, used if HSComPort is set to TRUE</param>
    ''' <returns>(Empty string)</returns>
    ''' <remarks>http://homeseer.com/support/homeseer/HS3/SDK/initialization_-_when_used.htm</remarks>
    Public Function InitIO(ByVal port As String) As String
        Debug(5, "Plugin", "Starting InitIO initializiation.")

        'Loading settings before we do anything else
        Me.Settings = New Settings

        'Registering two pages
        RegisterWebPage(link:=configPageName, linktext:="Config", page_title:="Configuration")
        'RegisterWebPage(link:=supportPageName, linktext:="Support", page_title:="Support")

        'RegisterWebPage(link:=statusPageName, linktext:="", page_title:="Demo test")

        'Adding a trigger 
        'triggers.Add(CObj(Nothing), "Random value is lower than")

        'Adding a second trigger with subtriggers
        '... so first let us create the subtriggers
        'Dim subtriggers As New Trigger
        'subtriggers.Add(CObj(Nothing), "Random value is lower than")
        'subtriggers.Add(CObj(Nothing), "Random value is equal to")
        'subtriggers.Add(CObj(Nothing), "Random value is higher than")

        '... and then the trigger with the subtriggers
        'triggers.Add(subtriggers, "Random value is...")

        'Adding an action
        'actions.Add(CObj(Nothing), "Send a custom command somewhere")

        'Checks if plugin devices are present, and create them if not.
        'CheckAndCreateDevices()


        'Starting the update timer; a timer for fetching updates from the web (for example). However, in this sample, the UpdateTimerTrigger just generates a random value. (Should ideally be placed in its own thread, but I use a Timer here for simplicity).
        'updateTimer = New Threading.Timer(AddressOf UpdateRandomValue, Nothing, Timeout.Infinite, Me.Settings.TimerInterval)
        'RestartTimer()
        Debug(5, "Plugin", "Starting Version " + Version + " " + Release)
        Debug(5, "Plugin", "Developed by Richard Taylor (109)")
        Debug(5, "Plugin", "Kudos to Theo Arends for Tasmota and Hamidreza Mohaqeq for M2MQTT")
        Debug(5, "Plugin", "-----------------------------------------------------------------")
        GlobalVariables.DebugLevel = Settings.IniDebugLevel
        If Settings.RawLogFile <> "" Then
            Try
                GlobalVariables.RawLog = New System.IO.StreamWriter(Settings.RawLogFile, False)
                GlobalVariables.RawLogFileName = Settings.RawLogFile
                Debug(5, "Plugin", "Opened Log " + Settings.RawLogFile.ToString)
                GlobalVariables.LogFile = True
            Catch ex As Exception
                Debug(5, "Plugin", "Failed to Open Logfile " + Settings.RawLogFile.ToString + " Error " + ex.Message.ToString)
            End Try

        End If

        ReadDevices()

        callback.RegisterEventCB(Enums.HSEvent.VALUE_CHANGE, IFACE_NAME, "")
        Debug(5, "Plugin", "Debug Level " + GlobalVariables.DebugLevel.ToString)
        Debug(5, "Plugin", "DoUpdate " + GlobalVariables.DoUpdate.ToString)
        Debug(5, "Plugin", "Launching Threads")

        Dim myCallBack As New System.Threading.TimerCallback(AddressOf QueueProcessing.ProcessQueue)
        myTimer1 = New System.Threading.Timer(myCallBack, Nothing, 100, 2000)
        AddWarning(0, "Enabled Event Queue Processing Threads")

        Dim MyHouse As New System.Threading.TimerCallback(AddressOf Housekeeping.Maintenance)
        MyTimer2 = New System.Threading.Timer(MyHouse, Nothing, 100, 30000)
        AddWarning(0, "Enabled Maintenance Threads")




        Debug(5, "Plugin", "Ready")

        Return ""
    End Function

    Sub ReadDevices()
        Dim DType As UInt16 = 0
        GlobalVariables.TasmotaDevices.Clear()
        'GlobalVariables.MQTTDevices.Clear()

        Dim devs = (From d In Devices()).ToList
        Dim DeviceType As String = ""

        For Each dev In (From d In devs Where d.Device_Type_String(hs).Contains("MQTT:"))
            DeviceType = Trim(dev.Device_Type_String(hs).ToString)
            Debug(1, "Plugin", "Device Found " + dev.Ref(hs).ToString + " = " + DeviceType)
            DeviceType = Replace(DeviceType, "MQTT:", "")
            Dim DeviceItems = Split(DeviceType, "/")
            If UBound(DeviceItems) > 0 Then
                If InStr(DeviceItems(1), "POWER") > 0 Then
                    DType = 0 'Power Output Device
                Else
                    DType = 1 ' JSON Data Extrator
                End If
                GlobalVariables.TasmotaDevices.Add(New DeviceInfo() With {.DeviceID = dev.Ref(hs), .DeviceString = DeviceType, .DeviceName = DeviceItems(0), .HSDeviceName = dev.Name(hs).ToString, .DeviceTarget = DeviceItems(1), .Active = False, .Type = DType, .IgnoreTele = False})
                Debug(1, "Plugin", "Device Added " + dev.Ref(hs).ToString + " : " + DeviceType + " [" + DeviceItems(0).ToString + "][" + DeviceItems(1).ToString + "]")
            Else
                Debug(1, "Plugin", "Malformed Device Definition [" + DeviceType + "]")
            End If
        Next

        For Each TasDevices As DeviceInfo In GlobalVariables.TasmotaDevices
            Debug(5, "Plugin", TasDevices.DeviceID.ToString() + " " + TasDevices.HSDeviceName + " = DeviceString [" + TasDevices.DeviceString + "] DeviceName [" + TasDevices.DeviceName + "] DeviceTarget [" + TasDevices.DeviceTarget + "] [ Active " + TasDevices.Active.ToString + ", LastSeen " + TasDevices.LastSeen.ToString + "]")
            GlobalVariables.MQTTQ.Enqueue("TelePeriod|" + TasDevices.DeviceName + "|60")
        Next
    End Sub


    Sub HSEvent(ByVal EventType As Enums.HSEvent, ByVal parms() As Object) Implements HomeSeerAPI.IPlugInAPI.HSEvent

    End Sub
    ''' <summary>
    ''' When HomeSeer shuts down or a plug-in is disabled from the interfaces page this function is then called. You should terminate any threads that you started, close any COM ports or TCP connections and release memory.
    ''' After you return from this function the plugin EXE will terminate and must be allowed to terminate cleanly.
    ''' </summary>
    ''' <remarks>http://homeseer.com/support/homeseer/HS3/SDK/initialization_-_when_used.htm</remarks>
    Public Sub ShutdownIO()
        Try
            ''**********************
            ''For debugging only, this will delete all devices accociated by the plugin at shutdown, so new devices will be created on startup:
            ' DeleteDevices()
            ''**********************

            'Setting a flag that states that we are shutting down, this can be used to abort ongoing commands
            GlobalVariables.IsShuttingDown = True
            GlobalVariables.EnableQueue = False

            'Write any changes in the settings clas to the ini file
            'Me.Settings.Save()

            'Stopping the timer if it exists and runs
            If updateTimer IsNot Nothing Then
                updateTimer.Change(Timeout.Infinite, Timeout.Infinite)
                updateTimer.Dispose()
            End If

            'Save all device changes on plugin shutdown
            'Try
            'hs.SaveEventsDevices()
            'Catch ex As Exception
            'Debug(9, System.Reflection.MethodBase.GetCurrentMethod().Name, "could not save devices")
            'End Try
            callback.UnRegisterGenericEventCB(Enums.HSEvent.VALUE_CHANGE, IFACE_NAME, "")
            myTimer1.Change(Threading.Timeout.Infinite, Threading.Timeout.Infinite)
            MyTimer2.Change(Threading.Timeout.Infinite, Threading.Timeout.Infinite)
            MQTT.CloseMQTT()


        Catch ex As Exception
            Debug(9, System.Reflection.MethodBase.GetCurrentMethod().Name, "Error ending " & Me.Name & " Plug-In")
        End Try
        Debug(5, "Plugin", "ShutdownIO complete.")
        If GlobalVariables.LogFile Then
            GlobalVariables.RawLog.Close()
        End If
    End Sub


#End Region

#Region "Action/Trigger/DeviceConfig Processes"

#Region "Device Config Interface"

    ''' <summary>
    ''' If SupportsConfigDevice returns TRUE, this function will be called when the device properties are displayed for your device. This functions creates a tab for each plug-in that controls the device.
    ''' 
    ''' If the newDevice parameter is TRUE, the user is adding a new device from the HomeSeer user interface.
    ''' If you return TRUE from your SupportsAddDevice then ConfigDevice will be called when a user is creating a new device.
    ''' Your tab will appear and you can supply controls for the user to create a new device for your plugin. When your ConfigDevicePost is called you will need to get a reference to the device using the past ref number and then take ownership of the device by setting the interface property of the device to the name of your plugin. You can also set any other properties on the device as needed.
    ''' </summary>
    ''' <param name="ref">The device reference number</param>
    ''' <param name="user">The user that is logged into the server and viewing the page</param>
    ''' <param name="userRights">The rights of the logged in user</param>
    ''' <param name="newDevice">True if this a new device being created for the first time. In this case, the device configuration dialog may present different information than when simply editing an existing device.</param>
    ''' <returns>A string containing HTML to be displayed. Return an empty string if there is not configuration needed.</returns>
    ''' <remarks>http://homeseer.com/support/homeseer/HS3/SDK/configdevice.htm</remarks>
    Public Function ConfigDevice(ref As Integer, user As String, userRights As Integer, newDevice As Boolean) As String
        Dim device As Scheduler.Classes.DeviceClass = Nothing
        Dim stb As New StringBuilder

        device = hs.GetDeviceByRef(ref)

        Dim PED As clsPlugExtraData = device.PlugExtraData_Get(hs)
        Dim PEDname As String = Me.Name

        'We'll use the device type string to determine how we should handle the device in the plugin
        Select Case device.Device_Type_String(hs).Replace(Me.Name, "").Trim
            Case ""
                '======================================================
                'It's a device created by the HSPI_SAMPLE_BASIC setting, and is included for reference only.
                'Its not used by this sample. See further down for "Basic" and "Advanced".
                '======================================================

                Dim sample As SampleClass = PEDGet(PED, PEDname)
                Dim houseCodeDropDownList As New clsJQuery.jqDropList("HouseCode", "", False)
                Dim unitCodeDropDownList As New clsJQuery.jqDropList("DeviceCode", "", False)
                Dim saveButton As New clsJQuery.jqButton("Save", "Done", "DeviceUtility", True)
                Dim houseCode As String = ""
                Dim deviceCode As String = ""


                If sample Is Nothing Then
                    Console.WriteLine("ConfigDevice, sample is nothing")
                    ' Set the defaults
                    sample = New SampleClass
                    InitHSDevice(device, device.Name(hs))
                    sample.houseCode = "A"
                    sample.deviceCode = "1"
                    PEDAdd(PED, PEDname, sample)
                    device.PlugExtraData_Set(hs) = PED
                End If

                houseCode = sample.houseCode
                deviceCode = sample.deviceCode

                For Each l In "ABCDEFGHIJKLMNOP"
                    houseCodeDropDownList.AddItem(l, l, l = houseCode)
                Next
                For i = 1 To 16
                    unitCodeDropDownList.AddItem(i.ToString, i.ToString, i.ToString = deviceCode)
                Next

                Try
                    stb.Append("<form id='frmSample' name='SampleTab' method='Post'>")
                    stb.Append(" <table border='0' cellpadding='0' cellspacing='0' width='610'>")
                    stb.Append("  <tr><td colspan='4' align='Center' style='font-size:10pt; height:30px;' nowrap>Select a houseCode and Unitcode that matches one of the devices HomeSeer will be communicating with.</td></tr>")
                    stb.Append("  <tr>")
                    stb.Append("   <td nowrap class='tablecolumn' align='center' width='70'>House<br>Code</td>")
                    stb.Append("   <td nowrap class='tablecolumn' align='center' width='70'>Unit<br>Code</td>")
                    stb.Append("   <td nowrap class='tablecolumn' align='center' width='200'>&nbsp;</td>")
                    stb.Append("  </tr>")
                    stb.Append("  <tr>")
                    stb.Append("   <td class='tablerowodd' align='center'>" & houseCodeDropDownList.Build & "</td>")
                    stb.Append("   <td class='tablerowodd' align='center'>" & unitCodeDropDownList.Build & "</td>")
                    stb.Append("   <td class='tablerowodd' align='left'>" & saveButton.Build & "</td>")
                    stb.Append("  </tr>")
                    stb.Append(" </table>")
                    stb.Append("</form>")
                    Return stb.ToString
                Catch ex As Exception
                    Return "ConfigDevice ERROR: " & ex.Message 'Original is too old school: "Return Err.Description"
                End Try


            Case "Basic"
                stb.Append("<form id='frmSample' name='SampleTab' method='Post'>")
                stb.Append("Nothing special to configure for the basic device. :-)")
                stb.Append("</form>")
                Return stb.ToString

            Case "Advanced"
                Dim savedString As String = PEDGet(PED, PEDname)
                If savedString = String.Empty Then 'The pluginextradata is not configured for this device
                    savedString = "The text in this textbox is saved with the actual device"
                End If

                Dim savedTextbox As New clsJQuery.jqTextBox("savedTextbox", "", savedString, "", 100, False)
                Dim saveButton As New clsJQuery.jqButton("Save", "Done", "DeviceUtility", True)

                stb.Append("<form id='frmSample' name='SampleTab' method='Post'>")
                stb.Append(" <table border='0' cellpadding='0' cellspacing='0' width='610'>")
                stb.Append("  <tr><td colspan='4' align='Center' style='font-size:10pt; height:30px;' nowrap>Text to be saved with the device.</td></tr>")
                stb.Append("  <tr>")
                stb.Append("   <td nowrap class='tablecolumn' align='center' width='70'>Text:</td>")
                stb.Append("   <td nowrap class='tablecolumn' align='center' width='200'>&nbsp;</td>")
                stb.Append("  </tr>")
                stb.Append("  <tr>")
                stb.Append("   <td class='tablerowodd' align='center'>" & savedTextbox.Build & "</td>")
                stb.Append("   <td class='tablerowodd' align='left'>" & saveButton.Build & "</td>")
                stb.Append("  </tr>")
                stb.Append(" </table>")
                stb.Append("</form>")

                Return stb.ToString
        End Select

        Return String.Empty
    End Function

    ''' <summary>
    ''' This function is called when a user posts information from your plugin tab on the device utility page
    ''' </summary>
    ''' <param name="ref">The device reference</param>
    ''' <param name="data">query string data posted to the web server (name/value pairs from controls on the page)</param>
    ''' <param name="user">The user that is logged into the server and viewing the page</param>
    ''' <param name="userRights">The rights of the logged in user</param>
    ''' <returns>
    ''' DoneAndSave = 1            Any changes to the config are saved and the page is closed and the user it returned to the device utility page
    ''' DoneAndCancel = 2          Changes are not saved and the user is returned to the device utility page
    ''' DoneAndCancelAndStay = 3   No action is taken, the user remains on the plugin tab
    ''' CallbackOnce = 4           Your plugin ConfigDevice is called so your tab can be refereshed, the user stays on the plugin tab
    ''' CallbackTimer = 5          Your plugin ConfigDevice is called and a page timer is called so ConfigDevicePost is called back every 2 seconds
    ''' </returns>
    ''' <remarks>http://homeseer.com/support/homeseer/HS3/SDK/configdevicepost.htm</remarks>
    Public Function ConfigDevicePost(ref As Integer, data As String, user As String, userRights As Integer) As Enums.ConfigDevicePostReturn
        Dim ReturnValue As Integer = Enums.ConfigDevicePostReturn.DoneAndCancel

        Try
            Dim device As Scheduler.Classes.DeviceClass = hs.GetDeviceByRef(ref)
            Dim PED As clsPlugExtraData = device.PlugExtraData_Get(hs)
            Dim PEDname As String = Me.Name
            Dim parts As Collections.Specialized.NameValueCollection = HttpUtility.ParseQueryString(data)

            'We'll use the device type string to determine how we should handle the device in the plugin
            Select Case device.Device_Type_String(hs).Replace(Me.Name, "").Trim
                Case ""
                    '===============================================================================
                    'It's a device created by HSPI_SAMPLE_BASIC (the old code), kept as a reference.
                    '===============================================================================
                    Dim sample As SampleClass = PEDGet(PED, PEDname)
                    If sample Is Nothing Then
                        InitHSDevice(device)
                    End If

                    sample.houseCode = parts("HouseCode")
                    sample.deviceCode = parts("DeviceCode")

                    PED = device.PlugExtraData_Get(hs)
                    PEDAdd(PED, PEDname, sample)
                    device.PlugExtraData_Set(hs) = PED
                    hs.SaveEventsDevices()

                Case "Basic"
                    'Nothing to store as this device doesn't have any extra data to save

                Case "Advanced"
                    'We'll get the string to save from the postback values
                    Dim savedString As String = parts("savedTextbox")

                    'We'll save this to the pluginextradata storage
                    PED = device.PlugExtraData_Get(hs)
                    PEDAdd(PED, PEDname, savedString) 'Adds the saveString to the plugin if it doesn't exist, and removes and adds it if it does.
                    device.PlugExtraData_Set(hs) = PED

                    'And then finally save the device
                    hs.SaveEventsDevices()

            End Select

            Return ReturnValue
        Catch ex As Exception
            Debug(9, System.Reflection.MethodBase.GetCurrentMethod().Name, "ConfigDevicePost: " & ex.Message)
        End Try
        Return ReturnValue
    End Function

    ''' <summary>
    ''' SetIOMulti is called by HomeSeer when a device that your plug-in owns is controlled.
    ''' Your plug-in owns a device when it's INTERFACE property is set to the name of your plug
    ''' </summary>
    ''' <param name="colSend">
    ''' A collection of CAPIControl objects, one object for each device that needs to be controlled.
    ''' Look at the ControlValue property to get the value that device needs to be set to.</param>
    ''' <remarks>http://homeseer.com/support/homeseer/HS3/SDK/setio.htm</remarks>
    Public Sub SetIOMulti(ByVal colSend As List(Of HomeSeerAPI.CAPI.CAPIControl))
        'Multiple CAPIcontrols might be sent at the same time, so we need to check each one
        For Each CC In colSend
            Console.WriteLine("SetIOMulti triggered, checking CAPI '" & CC.Label & "' on device " & CC.Ref)

            'CAPI doesn't magically store the new devicevalue, and I believe there's good reason for that:
            '  The status of the device migth depend on some hardware giving the response that it has received the command,
            '  and perhaps with an other value (indicating a status equal to "Error" or whatever). In that case; send the command,
            '  wait for the answer (in a new thread, for example) and THEN update the device value
            'But here, we just update the value for the device
            hs.SetDeviceValueByRef(CC.Ref, CC.ControlValue, False)

            'Get the device sending the CAPIcontrol
            Dim device As Scheduler.Classes.DeviceClass = hs.GetDeviceByRef(CC.Ref)

            'We can get the PlugExtraData, if anything is stored in the device itself. What is stored is based on the device type.
            Select Case device.Device_Type_String(hs).Replace(Me.Name, "").Trim
                Case ""
                    '****************************************************************
                    'Again, this is the basic device from HSPI_SAMPLE_BASIC from HST
                    '****************************************************************
                    Dim PED As clsPlugExtraData = device.PlugExtraData_Get(hs)
                    Dim sample As SampleClass = PEDGet(PED, "Sample")
                    If sample IsNot Nothing Then
                        Dim houseCode As String = sample.houseCode
                        Dim Devicecode As String = sample.deviceCode
                        SendCommand(houseCode, Devicecode) 'The HSPI_SAMPE control, in utils.vb as an example (but it doesn't do anything)
                    End If


                Case "Basic"
                    'There's nothing stored in the basic device

                Case "Advanced"
                    'Here we could choose to do something with the text string stored in the device

                Case Else
                    'Nothing to do at the moment
            End Select

        Next
    End Sub

#End Region

#Region "Trigger Properties"

    Public ReadOnly Property HasTriggers() As Boolean
        Get
            Return (TriggerCount() > 0)
        End Get
    End Property

    Public ReadOnly Property HasConditions(TriggerNumber As Integer) As Boolean
        Get
            Return True
        End Get
    End Property

    Public Function TriggerCount() As Integer
        Return triggers.Count
    End Function

    Public ReadOnly Property SubTriggerCount(ByVal TriggerNumber As Integer) As Integer
        Get
            Dim trigger As Trigger
            If IsValidTrigger(TriggerNumber) Then
                trigger = triggers(TriggerNumber)
                If Not (trigger Is Nothing) Then
                    Return trigger.Count
                Else
                    Return 0
                End If
            Else
                Return 0
            End If
        End Get
    End Property

    Public ReadOnly Property TriggerName(ByVal TriggerNumber As Integer) As String
        Get
            If Not IsValidTrigger(TriggerNumber) Then
                Return ""
            Else
                Return Me.Name & ": " & triggers.Keys(TriggerNumber - 1)
            End If
        End Get
    End Property

    Public ReadOnly Property SubTriggerName(ByVal TriggerNumber As Integer, ByVal SubTriggerNumber As Integer) As String
        Get
            Dim trigger As Trigger
            If IsValidSubTrigger(TriggerNumber, SubTriggerNumber) Then
                trigger = triggers(TriggerNumber)
                Return Me.Name & ": " & trigger.Keys(SubTriggerNumber - 1)
            Else
                Return ""
            End If
        End Get
    End Property

    Friend Function IsValidTrigger(ByVal TrigIn As Integer) As Boolean
        If TrigIn > 0 AndAlso TrigIn <= triggers.Count Then
            Return True
        End If
        Return False
    End Function

    Public Function IsValidSubTrigger(ByVal TrigIn As Integer, ByVal SubTrigIn As Integer) As Boolean
        Dim trigger As Trigger = Nothing
        If TrigIn > 0 AndAlso TrigIn <= triggers.Count Then
            trigger = triggers(TrigIn)
            If Not (trigger Is Nothing) Then
                If SubTrigIn > 0 AndAlso SubTrigIn <= trigger.Count Then Return True
            End If
        End If
        Return False
    End Function

#End Region

#Region "Trigger Interface"

    Private Enum TriggerTypes
        WithoutSubtriggers = 1
        WithSubtriggers = 2
    End Enum

    Private Enum SubTriggerTypes
        LowerThan = 1
        EqualTo = 2
        HigherThan = 3
    End Enum


    'Public Sub TriggerFire(ByVal TrigInfo As HomeSeerAPI.IPlugInAPI.strTrigActInfo)
    '    Try
    '        callback.TriggerFire(Me.Name, TrigInfo)
    '        Console.WriteLine("TriggerFire: Fired on event with ref = " & TrigInfo.evRef & ("  (name: " & (From ee In Events() Where ee.Event_Ref = TrigInfo.evRef).First.Event_Name) & ")")
    '    Catch ex As Exception
    '        Log("Error while running trigger: " & ex.Message, LogType.Error)
    '    End Try

    'End Sub

    '''' <summary>
    '''' Triggers notify HomeSeer of trigger states using TriggerFire , but Triggers can also be conditions, and that is where this is used.
    '''' If this function is called, TrigInfo will contain the trigger information pertaining to a trigger used as a condition.
    '''' 
    '''' Moskus: This is the function that determines if your trigger is true or false WHEN USED AS A CONDITION.
    '''' </summary>
    '''' <param name="TrigInfo">The trigger information</param>
    '''' <returns>True/False</returns>
    '''' <remarks>http://homeseer.com/support/homeseer/HS3/SDK/triggertrue.htm</remarks>
    Function TriggerTrue(TrigInfo As IPlugInAPI.strTrigActInfo) As Boolean
        'Let's specify the key name of the value we are looking for
        Dim key As String = "SomeValue"

        'Get the value from the trigger
        Dim triggervalue As Integer = GetTriggerValue(key, TrigInfo)

        Console.WriteLine("Conditional value found for " & key & ": " & triggervalue & vbTab & "Last random: " & lastRandomNumber)

        'Let's return if this condition is True or False
        Return (triggervalue >= lastRandomNumber)
    End Function

    ''' <summary>
    ''' Given a strTrigActInfo object detect if this this trigger is configured properly, if so, return True, else False.
    ''' </summary>
    ''' <param name="TrigInfo">Trigger information of "strTrigActInfo" (which is funny, as it isn't a string at all)</param>
    ''' <returns>True/False</returns>
    ''' <remarks>http://homeseer.com/support/homeseer/HS3/SDK/triggerconfigured.htm</remarks>
    Public ReadOnly Property TriggerConfigured(ByVal TrigInfo As HomeSeerAPI.IPlugInAPI.strTrigActInfo) As Boolean
        Get
            Dim itemsConfigured As Integer = 0
            Dim itemsToConfigure As Integer = 1
            Dim UID As String = TrigInfo.UID.ToString

            If Not (TrigInfo.DataIn Is Nothing) Then
                DeSerializeObject(TrigInfo.DataIn, trigger)
                For Each key As String In trigger.Keys
                    Select Case True
                        Case key.Contains("SomeValue_" & UID) AndAlso trigger(key) <> ""
                            itemsConfigured += 1
                    End Select
                Next
                If itemsConfigured = itemsToConfigure Then Return True
            End If

            Return False
        End Get
    End Property

    ''' <summary>
    ''' Return HTML controls for a given trigger.
    ''' </summary>
    ''' <param name="sUnique">Apparently some unique string</param>
    ''' <param name="TrigInfo">Trigger information</param>
    ''' <returns>Return HTML controls for a given trigger.</returns>
    ''' <remarks>http://homeseer.com/support/homeseer/HS3/SDK/triggerbuildui.htm</remarks>
    Public Function TriggerBuildUI(ByVal sUnique As String, ByVal TrigInfo As HomeSeerAPI.IPlugInAPI.strTrigActInfo) As String
        Dim UID As String = TrigInfo.UID.ToString
        Dim stb As New StringBuilder
        Dim someValue As Integer = -1 'We'll set the default value. This value will indicate that this trigger isn't properly configured
        Dim dd As New clsJQuery.jqDropList("SomeValue_" & UID & sUnique, Pagename, True)

        dd.autoPostBack = True
        dd.AddItem("--Please Select--", -1, False) 'A selected option with the default value (-1) means that the trigger isn't configured

        If Not (TrigInfo.DataIn Is Nothing) Then
            DeSerializeObject(TrigInfo.DataIn, trigger)
        Else 'new event, so clean out the trigger object
            trigger = New Trigger
        End If

        For Each key As String In trigger.Keys
            Select Case True

                'We'll fetch the selected value if this trigger has been configured before.
                Case key.Contains("SomeValue_" & UID)
                    someValue = trigger(key)
            End Select
        Next

        'We'll add all the different selectable values (numbers from 0 to 100 with 10 in increments)
        'and we'll select the option that was selected before if it's an old value (see ("i = someValue") which will be true or false)
        For i As Integer = 0 To 100 Step 10
            dd.AddItem(p_name:=i, p_value:=i, p_selected:=(i = someValue))
        Next

        'Finally we'll add this to the stringbuilder, and return the value
        stb.Append("Select value:")
        stb.Append(dd.Build)

        Return stb.ToString
    End Function

    ''' <summary>
    ''' Process a post from the events web page when a user modifies any of the controls related to a plugin trigger. After processing the user selctions, create and return a strMultiReturn object.
    ''' </summary>
    ''' <param name="PostData">The PostData as NameValueCollection</param>
    ''' <param name="TrigInfo">The trigger information</param>
    ''' <returns>A structure, which is used in the Trigger and Action ProcessPostUI procedures, which not only communications trigger and action information through TrigActInfo which is strTrigActInfo , but provides an array of Byte where an updated/serialized trigger or action object from your plug-in can be stored.  See TriggerProcessPostUI and ActionProcessPostUI for more details.</returns>
    ''' <remarks>http://homeseer.com/support/homeseer/HS3/SDK/triggerprocesspostui.htm</remarks>
    Public Function TriggerProcessPostUI(ByVal PostData As System.Collections.Specialized.NameValueCollection, ByVal TrigInfo As HomeSeerAPI.IPlugInAPI.strTrigActInfo) As HomeSeerAPI.IPlugInAPI.strMultiReturn
        Dim Ret As New HomeSeerAPI.IPlugInAPI.strMultiReturn
        Dim UID As String = TrigInfo.UID.ToString

        Ret.sResult = ""
        ' HST: We cannot be passed info ByRef from HomeSeer, so turn right around and return this same value so that if we want, 
        '   we can exit here by returning 'Ret', all ready to go.  If in this procedure we need to change DataOut or TrigInfo,
        '   we can still do that.
        Ret.DataOut = TrigInfo.DataIn
        Ret.TrigActInfo = TrigInfo

        If PostData Is Nothing Then Return Ret
        If PostData.Count < 1 Then Return Ret

        If Not (TrigInfo.DataIn Is Nothing) Then
            DeSerializeObject(TrigInfo.DataIn, trigger)
        End If

        Dim parts As Collections.Specialized.NameValueCollection
        parts = PostData
        Try
            For Each key As String In parts.Keys
                If key Is Nothing Then Continue For
                If String.IsNullOrEmpty(key.Trim) Then Continue For
                Select Case True
                    Case key.Contains("SomeValue_" & UID)
                        trigger.Add(CObj(parts(key)), key)
                End Select
            Next
            If Not SerializeObject(trigger, Ret.DataOut) Then
                Ret.sResult = Me.Name & " Error, Serialization failed. Signal Trigger not added."
                Return Ret
            End If
        Catch ex As Exception
            Ret.sResult = "ERROR, Exception in Trigger UI of " & Me.Name & ": " & ex.Message
            Return Ret
        End Try

        ' All OK
        Ret.sResult = ""
        Return Ret
    End Function

    ''' <summary>
    ''' After the trigger has been configured, this function is called in your plugin to display the configured trigger. Return text that describes the given trigger.
    ''' </summary>
    ''' <param name="TrigInfo">Information of the trigger</param>
    ''' <returns>Text describing the trigger</returns>
    ''' <remarks>http://homeseer.com/support/homeseer/HS3/SDK/triggerformatui.htm</remarks>
    Public Function TriggerFormatUI(ByVal TrigInfo As HomeSeerAPI.IPlugInAPI.strTrigActInfo) As String
        Dim stb As New StringBuilder
        Dim key As String
        Dim someValue As String = ""
        Dim UID As String = TrigInfo.UID.ToString

        If Not (TrigInfo.DataIn Is Nothing) Then
            DeSerializeObject(TrigInfo.DataIn, trigger)
        End If

        For Each key In trigger.Keys
            Select Case True
                Case key.Contains("SomeValue_" & UID)
                    someValue = trigger(key)
            End Select
        Next

        'We need different texts based on which trigger was used.
        Select Case TrigInfo.TANumber

            Case TriggerTypes.WithoutSubtriggers '= 1. The trigger without subtriggers only has one option:
                stb.Append(" the random value generator picks a number lower than " & someValue)

            Case TriggerTypes.WithSubtriggers '= 2. This trigger has subtriggers which also reflects on how the trigger is presented

                'let's start with the regular text for the trigger
                stb.Append(" the random value generator picks a number ")

                '... add the comparer (all subtriggers for the current trigger)
                Select Case TrigInfo.SubTANumber
                    Case SubTriggerTypes.LowerThan '= 1
                        stb.Append("lower than ")

                    Case SubTriggerTypes.EqualTo '= 2
                        stb.Append("equal to ")

                    Case SubTriggerTypes.HigherThan '3
                        stb.Append("higher than ")
                End Select

                '... and end with the selected value
                stb.Append(someValue)
        End Select


        hs.SaveEventsDevices()
        Return stb.ToString
    End Function

#End Region

#Region "Action Properties"

    ''' <summary>
    ''' Return the number of actions the plugin supports.
    ''' </summary>
    ''' <returns>The plugin count</returns>
    ''' <remarks>http://homeseer.com/support/homeseer/HS3/SDK/actioncount.htm</remarks>
    Function ActionCount() As Integer
        Return actions.Count
    End Function

    ''' <summary>
    ''' Return the name of the action given an action number. The name of the action will be displayed in the HomeSeer events actions list.
    ''' </summary>
    ''' <param name="ActionNumber">The number of the action. Each action is numbered, starting at 1. (BUT WHY 1?!)</param>
    ''' <returns>The action name from the 1-based index</returns>
    ''' <remarks>http://homeseer.com/support/homeseer/HS3/SDK/actionname.htm</remarks>
    ReadOnly Property ActionName(ByVal ActionNumber As Integer) As String
        Get
            If ActionNumber > 0 AndAlso ActionNumber <= actions.Count Then
                Return Me.Name & ": " & actions.Keys(ActionNumber - 1)
            Else
                Return ""
            End If
        End Get
    End Property

    Private ReadOnly Property IPlugInAPI_Name As String Implements IPlugInAPI.Name
        Get
            Throw New NotImplementedException()
        End Get
    End Property

    Public ReadOnly Property HSCOMPort As Boolean Implements IPlugInAPI.HSCOMPort
        Get
            Throw New NotImplementedException()
        End Get
    End Property

    Public Property ActionAdvancedMode As Boolean Implements IPlugInAPI.ActionAdvancedMode
        Get
            Throw New NotImplementedException()
        End Get
        Set(value As Boolean)
            Throw New NotImplementedException()
        End Set
    End Property

    Private ReadOnly Property IPlugInAPI_ActionName(ActionNumber As Integer) As String Implements IPlugInAPI.ActionName
        Get
            Throw New NotImplementedException()
        End Get
    End Property

    Private ReadOnly Property IPlugInAPI_HasConditions(TriggerNumber As Integer) As Boolean Implements IPlugInAPI.HasConditions
        Get
            Throw New NotImplementedException()
        End Get
    End Property

    Private ReadOnly Property IPlugInAPI_HasTriggers As Boolean Implements IPlugInAPI.HasTriggers
        Get
            Throw New NotImplementedException()
        End Get
    End Property

    Private ReadOnly Property IPlugInAPI_TriggerCount As Integer Implements IPlugInAPI.TriggerCount
        Get
            Throw New NotImplementedException()
        End Get
    End Property

    Private ReadOnly Property IPlugInAPI_TriggerName(TriggerNumber As Integer) As String Implements IPlugInAPI.TriggerName
        Get
            Throw New NotImplementedException()
        End Get
    End Property

    Private ReadOnly Property IPlugInAPI_SubTriggerCount(TriggerNumber As Integer) As Integer Implements IPlugInAPI.SubTriggerCount
        Get
            Throw New NotImplementedException()
        End Get
    End Property

    Private ReadOnly Property IPlugInAPI_SubTriggerName(TriggerNumber As Integer, SubTriggerNumber As Integer) As String Implements IPlugInAPI.SubTriggerName
        Get
            Throw New NotImplementedException()
        End Get
    End Property

    Private ReadOnly Property IPlugInAPI_TriggerConfigured(TrigInfo As IPlugInAPI.strTrigActInfo) As Boolean Implements IPlugInAPI.TriggerConfigured
        Get
            Throw New NotImplementedException()
        End Get
    End Property

    Public Property Condition(TrigInfo As IPlugInAPI.strTrigActInfo) As Boolean Implements IPlugInAPI.Condition
        Get
            Throw New NotImplementedException()
        End Get
        Set(value As Boolean)
            Throw New NotImplementedException()
        End Set
    End Property

#End Region

#Region "Action Interface"

    ''' <summary>
    ''' When an event is triggered, this function is called to carry out the selected action.
    ''' </summary>
    ''' <param name="ActInfo">Use the ActInfo parameter to determine what action needs to be executed then execute this action.</param>
    ''' <returns>Return TRUE if the action was executed successfully, else FALSE if there was an error.</returns>
    ''' <remarks>http://homeseer.com/support/homeseer/HS3/SDK/handleaction.htm</remarks>
    Public Function HandleAction(ByVal ActInfo As IPlugInAPI.strTrigActInfo) As Boolean
        Dim houseCode As String = ""
        Dim deviceCode As String = ""
        Dim UID As String = ActInfo.UID.ToString

        Try
            If Not (ActInfo.DataIn Is Nothing) Then
                DeSerializeObject(ActInfo.DataIn, action)
            Else
                Return False
            End If

            For Each key As String In action.Keys
                Select Case True
                    Case key.Contains("HouseCodes_" & UID)
                        houseCode = action(key)
                    Case key.Contains("DeviceCodes_" & UID)
                        deviceCode = action(key)
                End Select
            Next

            Console.WriteLine("HandleAction, Command received with data: " & houseCode & ", " & deviceCode)
            SendCommand(houseCode, deviceCode) 'This could also return a value True/False if it was successful or not

        Catch ex As Exception
            Debug(9, System.Reflection.MethodBase.GetCurrentMethod().Name, "Error executing action: " & ex.Message)
        End Try
        Return True
    End Function

    ''' <summary>
    ''' Return TRUE if the given action is configured properly. There may be times when a user can select invalid selections for the action and in this case you would return FALSE so HomeSeer will not allow the action to be saved.
    ''' </summary>
    ''' <param name="ActInfo">Object that contains information about the action like current selections.</param>
    ''' <returns>Return TRUE if the given action is configured properly.</returns>
    ''' <remarks>http://homeseer.com/support/homeseer/HS3/SDK/actionconfigured.htm</remarks>
    Public Function ActionConfigured(ByVal ActInfo As IPlugInAPI.strTrigActInfo) As Boolean
        Dim Configured As Boolean = False
        Dim itemsConfigured As Integer = 0
        Dim itemsToConfigure As Integer = 2
        Dim UID As String = ActInfo.UID.ToString

        If Not (ActInfo.DataIn Is Nothing) Then
            DeSerializeObject(ActInfo.DataIn, action)
            For Each key In action.Keys
                Select Case True
                    Case key.Contains("HouseCodes_" & UID) AndAlso action(key) <> ""
                        itemsConfigured += 1
                    Case key.Contains("DeviceCodes_" & UID) AndAlso action(key) <> ""
                        itemsConfigured += 1
                End Select
            Next
            If itemsConfigured = itemsToConfigure Then Configured = True
        End If
        Return Configured
    End Function

    ''' <summary>
    ''' This function is called from the HomeSeer event page when an event is in edit mode.
    ''' Your plug-in needs to return HTML controls so the user can make action selections.
    ''' Normally this is one of the HomeSeer jquery controls such as a clsJquery.jqueryCheckbox.
    ''' </summary>
    ''' <param name="sUnique">A unique string that can be used with your HTML controls to identify the control. All controls need to have a unique ID.</param>
    ''' <param name="ActInfo">Object that contains information about the action like current selections</param>
    ''' <returns> HTML controls that need to be displayed so the user can select the action parameters.</returns>
    ''' <remarks>http://homeseer.com/support/homeseer/HS3/SDK/actionbuildui.htm</remarks>
    Public Function ActionBuildUI(ByVal sUnique As String, ByVal ActInfo As HomeSeerAPI.IPlugInAPI.strTrigActInfo) As String
        Dim UID As String
        UID = ActInfo.UID.ToString
        Dim stb As New StringBuilder
        Dim Housecode As String = ""
        Dim DeviceCode As String = ""
        Dim dd As New clsJQuery.jqDropList("HouseCodes_" & UID & sUnique, Pagename, True)
        Dim dd1 As New clsJQuery.jqDropList("DeviceCodes_" & UID & sUnique, Pagename, True)
        Dim key As String


        dd.autoPostBack = True
        dd.AddItem("--Please Select--", "", False)
        dd1.autoPostBack = True
        dd1.AddItem("--Please Select--", "", False)

        If Not (ActInfo.DataIn Is Nothing) Then
            DeSerializeObject(ActInfo.DataIn, action)
        Else 'new event, so clean out the action object
            action = New Action
        End If

        For Each key In action.Keys
            Select Case True
                Case key.Contains("HouseCodes_" & UID)
                    Housecode = action(key)
                Case key.Contains("DeviceCodes_" & UID)
                    DeviceCode = action(key)
            End Select
        Next

        For Each C In "ABCDEFGHIJKLMNOP"
            dd.AddItem(C, C, (C = Housecode))
        Next

        stb.Append("Select House Code:")
        stb.Append(dd.Build)

        dd1.AddItem("All", "All", ("All" = DeviceCode))
        For i = 1 To 16
            dd1.AddItem(i.ToString, i.ToString, (i.ToString = DeviceCode))
        Next

        stb.Append("Select Unit Code:")
        stb.Append(dd1.Build)

        Return stb.ToString
    End Function

    ''' <summary>
    ''' When a user edits your event actions in the HomeSeer events, this function is called to process the selections.
    ''' </summary>
    ''' <param name="PostData">A collection of name value pairs that include the user's selections.</param>
    ''' <param name="ActInfo">Object that contains information about the action as "strTrigActInfo" (which is funny, as it isn't a string at all)</param>
    ''' <returns>Object the holds the parsed information for the action. HomeSeer will save this information for you in the database.</returns>
    ''' <remarks>http://homeseer.com/support/homeseer/HS3/SDK/actionprocesspostui.htm</remarks>
    Public Function ActionProcessPostUI(ByVal PostData As Collections.Specialized.NameValueCollection, ByVal ActInfo As IPlugInAPI.strTrigActInfo) As IPlugInAPI.strMultiReturn
        Dim ret As New HomeSeerAPI.IPlugInAPI.strMultiReturn
        Dim UID As String = ActInfo.UID.ToString

        ret.sResult = ""
        'HS: We cannot be passed info ByRef from HomeSeer, so turn right around and return this same value so that if we want, 
        '   we can exit here by returning 'Ret', all ready to go.  If in this procedure we need to change DataOut or TrigInfo,
        '   we can still do that.
        ret.DataOut = ActInfo.DataIn
        ret.TrigActInfo = ActInfo

        If PostData Is Nothing Then Return ret
        If PostData.Count < 1 Then Return ret

        If Not (ActInfo.DataIn Is Nothing) Then
            DeSerializeObject(ActInfo.DataIn, action)
        End If

        Dim parts As Collections.Specialized.NameValueCollection = PostData

        Try
            For Each key As String In parts.Keys
                If key Is Nothing Then Continue For
                If String.IsNullOrEmpty(key.Trim) Then Continue For
                Select Case True
                    Case key.Contains("HouseCodes_" & UID), key.Contains("DeviceCodes_" & UID)
                        action.Add(CObj(parts(key)), key)
                End Select
            Next
            If Not SerializeObject(action, ret.DataOut) Then
                ret.sResult = Me.Name & " Error, Serialization failed. Signal Action not added."
                Return ret
            End If
        Catch ex As Exception
            ret.sResult = "ERROR, Exception in Action UI of " & Me.Name & ": " & ex.Message
            Return ret
        End Try

        ' All OK
        ret.sResult = ""
        Return ret
    End Function

    ''' <summary>
    ''' "Body of text here"... Okay, my guess:
    ''' This formats the chosen action when the action is "minimized" based on the user selected options
    ''' </summary>
    ''' <param name="ActInfo">Information from the current activity as "strTrigActInfo" (which is funny, as it isn't a string at all)</param>
    ''' <returns>Simple string. Possibly HTML-formated.</returns>
    ''' <remarks>http://homeseer.com/support/homeseer/HS3/SDK/actionformatui.htm</remarks>
    Public Function ActionFormatUI(ByVal ActInfo As IPlugInAPI.strTrigActInfo) As String
        Dim stb As New StringBuilder
        Dim houseCode As String = ""
        Dim deviceCode As String = ""
        Dim UID As String = ActInfo.UID.ToString

        If Not (ActInfo.DataIn Is Nothing) Then
            DeSerializeObject(ActInfo.DataIn, action)
        End If

        For Each key As String In action.Keys
            Select Case True
                Case key.Contains("HouseCodes_" & UID)
                    houseCode = action(key)
                Case key.Contains("DeviceCodes_" & UID)
                    deviceCode = action(key)
            End Select
        Next

        stb.Append(" the system will do 'something' to a device with ")
        stb.Append("HouseCode " & houseCode & " ")
        If deviceCode = "ALL" Then
            stb.Append("for all Unitcodes")
        Else
            stb.Append("and Unitcode " & deviceCode)
        End If

        Return stb.ToString
    End Function

#End Region

#End Region

#Region "HomeSeer-Required Functions"

    ''' <summary>
    ''' Returns the name of the plugin
    ''' </summary>
    ''' <returns></returns>
    ''' <remarks></remarks>
    Public Function Name() As String
        Return IFACE_NAME
    End Function

    ''' <summary>
    ''' Return the access level of this plug-in. Access level is the licensing mode.
    ''' </summary>
    ''' <returns>
    ''' 1 = Plug-in is not licensed and may be enabled and run without purchasing a license. Use this value for free plug-ins.
    ''' 2 = Plug-in is licensed and a user must purchase a license in order to use this plug-in. When the plug-in is first enabled, it will will run as a trial for 30 days.</returns>
    ''' <remarks>http://homeseer.com/support/homeseer/HS3/SDK/accesslevel.htm</remarks>
    Public Function AccessLevel() As Integer
        AccessLevel = 1
    End Function

#End Region

#Region "Web Page Processing"
    Private Function SelectPage(ByVal pageName As String) As Object
        Select Case pageName
            Case configPage.PageName
                Return configPage
                'Case statusPage.PageName
                'Return statusPage
            Case Else
                Return configPage
        End Select
        Return Nothing
    End Function

    Public Function PostBackProc(page As String, data As String, user As String, userRights As Integer) As String
        webPage = SelectPage(page)
        Return webPage.postBackProc(page, data, user, userRights)
    End Function

    Public Function GetPagePlugin(ByVal pageName As String, ByVal user As String, ByVal userRights As Integer, ByVal queryString As String) As String
        ' build and return the actual page
        Debug(4, "Web", "Build and Serve Page " & pageName)
        webPage = SelectPage(pageName)
        Return webPage.GetPagePlugin(pageName, user, userRights, queryString)
    End Function

#End Region

#Region "Timers, trigging triggers"

    'Private Sub UpdateRandomValue(ByVal obj As Object)
    '    '************
    '    'Random value
    '    '************
    '    'We need some data. So we're just creating a random value

    '    'Let's make a nice random number between 0 and 100
    '    Dim rnd As New Random(Now.Hour + Now.Minute + Now.Second + Now.Millisecond)
    '    Dim randomValue As Integer = rnd.Next(100)

    '    'The triggers can be used as an Condition, so we need to store the last value. "lastRandomNumber" is a class global variable.
    '    lastRandomNumber = randomValue

    '    'Let's write this random value to the log so that we can see what's going on, if the user has opted to do so
    '    If Me.Settings.LogTimerElapsed Then Log("Timer elapsed. Huzza! New random value: " & randomValue)


    '    '******
    '    'Events
    '    '******
    '    'Getting all triggers for this plugin (this only returns triggers where it is the FIRST option in an event, not when it's used as a condition)
    '    Dim triggers() As HomeSeerAPI.IPlugInAPI.strTrigActInfo = callback.GetTriggers(Me.Name)
    '    Console.WriteLine("UpdateTimer_Elapsed." & vbTab & "Triggers found: " & triggers.Count & vbTab & "Random value: " & randomValue)

    '    'Checking reach event with triggers from our plugin if they should be triggered or not
    '    For Each t In triggers

    '        'The name of the key we are looking for
    '        Dim key As String = "SomeValue"

    '        'Get the value from the trigger
    '        Dim triggerValue As Integer = GetTriggerValue(key, t)
    '        'Console.WriteLine("Value found for " & key & ": " & triggervalue) '... for debugging

    '        'Remember from TriggerBuildUI that if "someValue" is equal to "-1", we don't have a properly configured trigger, so we need to skip this if this (the current "t") value is -1
    '        If triggerValue = -1 Then
    '            Console.WriteLine("Event with ref = " & t.evRef & " is not properly configured.")
    '            Continue For
    '        End If

    '        'If not, we do have a working trigger, so let's continue

    '        'We have the option to select between two triggers, so we need to check them both
    '        Select Case t.TANumber
    '            Case TriggerTypes.WithoutSubtriggers  '= 1. The trigger without subtriggers

    '                'If the test is true, then trig the Trigger
    '                If triggerValue >= randomValue Then
    '                    Console.WriteLine("Trigging event with reference " & t.evRef)
    '                    TriggerFire(t)
    '                End If


    '            Case TriggerTypes.WithSubtriggers '= 2. The trigger with subtriggers

    '                'We have multiple options for checking for values, they are specified by the subtrigger number (1-based)
    '                Select Case t.SubTANumber
    '                    Case SubTriggerTypes.LowerThan 'The random value should be lower than the value specified in the event
    '                        If triggerValue >= randomValue Then
    '                            Console.WriteLine("Value is lower. Trigging event with reference " & t.evRef)
    '                            TriggerFire(t)
    '                        End If

    '                    Case SubTriggerTypes.EqualTo 'The random value should be equal to the value specified in the event
    '                        If triggerValue = randomValue Then
    '                            Console.WriteLine("Value is equal. Trigging event with reference " & t.evRef)
    '                            TriggerFire(t)
    '                        End If

    '                    Case SubTriggerTypes.HigherThan 'The random value should be higher than the value specified in the event
    '                        If triggerValue <= randomValue Then
    '                            Console.WriteLine("Value is higher. Trigging event with reference " & t.evRef)
    '                            TriggerFire(t)
    '                        End If

    '                    Case Else
    '                        Log("Undefined subtrigger!")

    '                End Select

    '            Case Else
    '                Log("Undefined trigger!")

    '        End Select

    '    Next


    '    '*******
    '    'DEVICES
    '    '*******
    '    'We get all the devices (and Linq is awesome!)
    '    Dim devs = (From d In Devices()
    '                Where d.Interface(hs) = Me.Name).ToList

    '    'In this example there are not any external sources that should update devices, so we're just updating the device value of the basic device and setting it to the new random value.

    '    'We do this for each "Basic" device we have (usually just one, but still...)
    '    For Each dev In (From d In devs
    '                     Where d.Device_Type_String(hs).Contains("Basic"))
    '        hs.SetDeviceValueByRef(dev.Ref(hs), randomValue, True)
    '        hs.SetDeviceString(dev.Ref(hs), "Last random value: " & randomValue, False)
    '    Next

    '    '... but of course we can do cooler stuff with our devices than that. E.g. add the text stored in the "Advanced Device" to the device string, for example

    '    'Again, for all "Advanced" devices
    '    For Each dev In (From d In devs Where d.Device_Type_String(hs).Contains("Advanced"))

    '        'Get the PlugExtraData class stored in the device
    '        Dim PED As clsPlugExtraData = dev.PlugExtraData_Get(hs)

    '        '... but we can only do something if there actually IS some PlugExtraData
    '        If PED IsNot Nothing Then

    '            'Get the value belonging to our plugin from the devices PlugExtraData 
    '            Dim savedString As String = PEDGet(PED, Me.Name)

    '            'TODO: Do something with the saved string and the random value?
    '            'hs.SetDeviceValueByRef(dev.Ref(hs), randomvalue, True)
    '            'hs.SetDeviceString(dev.Ref(hs), savedString & " - with value: " & randomvalue, True)
    '        End If
    '    Next

    'End Sub


    ''' <summary>
    ''' Get the actual value to check from a trigger
    ''' </summary>
    ''' <param name="key_to_find">The key stored in the trigger, like "SomeValue"</param>
    ''' <param name="TrigInfo">The trigger information to check</param>
    ''' <returns>Object of whatever is stored</returns>
    ''' <remarks>By Moskus</remarks>
    Private Function GetTriggerValue(ByVal key_to_find As String, ByVal TrigInfo As HomeSeerAPI.IPlugInAPI.strTrigActInfo) As Object
        Dim _trigger As New Trigger

        'Loads the trigger from the serialized object (if it exists, and it should)
        If Not (TrigInfo.DataIn Is Nothing) Then
            DeSerializeObject(TrigInfo.DataIn, _trigger)
        End If

        'A trigger has "keys" with different stored values, let's go through them all.
        'In my sample we only have one key, which is "SomeValue"
        For Each key In _trigger.Keys
            Select Case True
                Case key.Contains(key_to_find & "_" & TrigInfo.UID)
                    'We found the correct key, so let's just return the value:
                    Return _trigger(key)
            End Select
        Next

        'Apparently we didn't find any matching keys in the trigger, so that's all we have to return
        Return Nothing
    End Function

    'Public Sub RestartTimer()
    '    'Get now time
    '    Dim timeNow = Now.TimeOfDay

    '    'Round time to the nearest whole trigger (if Me.Settings.TimerInterval is set to 5 minutes, the trigger will be exectued 10:00, 10:05, 10:10, etc
    '    Dim nextWhole As TimeSpan = TimeSpan.FromMilliseconds(Math.Ceiling((Now.TimeOfDay.TotalMilliseconds) / Me.Settings.TimerInterval) * Me.Settings.TimerInterval)

    '    'Find the difference in milliseconds
    '    Dim diff As Long = nextWhole.Subtract(timeNow).TotalMilliseconds
    '    Console.WriteLine("RestartTimer, timeNow: " & timeNow.ToString)
    '    Console.WriteLine("RestartTimer, nextWhole: " & nextWhole.ToString)
    '    Console.WriteLine("RestartTimer, diff: " & diff)

    '    updateTimer.Change(diff, Me.Settings.TimerInterval)
    'End Sub
#End Region

#Region "Device creation and management"

    'Private Sub CheckAndCreateDevices()
    '    'Here we wil check if we have all the devices we want or if they should be created.
    '    'In this example I have said that we want to have:
    '    ' - One "Basic" device
    '    ' - One "Advanced" device with some controls
    '    ' - One "Root" (or Master) device with two child devices

    '    'HS usually use the deviceenumerator for this kind of stuff, but I prefer to use Linq.
    '    'As HS haven't provided a way to get a list (or "queryable method") for devices, I've made one (Check the function "Devices()" in utils.vb).
    '    'Here we are only interessted in the plugin devices for this plugin, so let's do some first hand filtering
    '    Dim devs = (From d In Devices()
    '                Where d.Interface(hs) = Me.Name).ToList


    '    'First let's see if we can find any devices belonging to the plugin with device type = "Basic". The device type string should contain "Basic".
    '    If (From d In devs Where d.Device_Type_String(hs).Contains("Basic")).Count = 0 Then
    '        'Apparently we don't have a basic device, so we create one with the name "Test basic device"
    '        CreateBasicDevice("Test basic device")
    '    End If


    '    'Then let's see if we can find the "Advanced" device, and create it if not
    '    If (From d In devs Where d.Device_Type_String(hs).Contains("Advanced")).Count = 0 Then
    '        CreateAdvancedDevice("Test advanced device")
    '    End If


    '    'Checking root devices and child devices
    '    If (From d In devs Where d.Device_Type_String(hs).Contains("Root")).Count = 0 Then

    '        'There are no root device so let's create one, and keep the device reference
    '        Dim rootDeviceReference As Integer = CreateRootDevice("Test Root device")
    '        Dim root As Scheduler.Classes.DeviceClass = hs.GetDeviceByRef(rootDeviceReference)

    '        'The point of a root/parent device is to have children, so let's have some fun creating them *badam tish*
    '        For i As Integer = 1 To 2
    '            'Let's create the child devivce
    '            Dim childDeviceReference As Integer = CreateChildDevice("Child " & i)

    '            '... and associate it with the root
    '            If childDeviceReference > 0 Then root.AssociatedDevice_Add(hs, childDeviceReference)
    '        Next

    '    Else
    '        'We have a root device or more, but do we have child devices?
    '        Dim roots = From d In devs
    '                    Where d.Device_Type_String(hs).Contains("Root")

    '        For Each root In roots
    '            'If we don't have two root devices... 
    '            If root.AssociatedDevices_Count(hs) <> 2 Then

    '                '...we delete them all
    '                For Each child In root.AssociatedDevices(hs)
    '                    hs.DeleteDevice(child)
    '                Next

    '                '... and recreate them
    '                For i As Integer = 1 To 2
    '                    'First create the device and get the reference
    '                    Dim childDeviceReference As Integer = CreateChildDevice("Child " & i)

    '                    'Then associated that child reference with the root.
    '                    If childDeviceReference > 0 Then root.AssociatedDevice_Add(hs, childDeviceReference)
    '                Next

    '                'NOTE: This could be handled more elegantly, like checking which child devices are missing, and creating only those.
    '            End If
    '        Next
    '    End If

    'End Sub

    'Private Function CreateBasicDevice(ByVal name As String) As Integer
    '    Try
    '        'Creating a brand new device, and get the actual device from the device reference
    '        Dim device As Scheduler.Classes.DeviceClass = hs.GetDeviceByRef(hs.NewDeviceRef(name))
    '        Dim reference As Integer = device.Ref(hs)

    '        'Setting the type to plugin device
    '        Dim typeInfo As New DeviceTypeInfo
    '        typeInfo.Device_Type = DeviceTypeInfo.eDeviceAPI.Plug_In
    '        device.DeviceType_Set(hs) = typeInfo
    '        device.Can_Dim(hs) = False

    '        device.Interface(hs) = Me.Name          'Don't change this, or the device won't be associated with your plugin
    '        device.InterfaceInstance(hs) = instance 'Don't change this, or the device won't be associated with that particular instance

    '        device.Device_Type_String(hs) = Me.Name & " " & "Basic" ' This you can change to something suitable, though. :)

    '        'Setting the name and locations
    '        device.Name(hs) = name 'as approved by input variable
    '        device.Location(hs) = Settings.Location
    '        device.Location2(hs) = Settings.Location2

    '        'Misc options
    '        device.Status_Support(hs) = False               'Set to True if the devices can be polled, False if not. (See PollDevice in hspi.vb)
    '        device.MISC_Set(hs, Enums.dvMISC.SHOW_VALUES)   'If not set, device control options will not be displayed.
    '        device.MISC_Set(hs, Enums.dvMISC.NO_LOG)        'As default, we don't want to log every device value change to the log

    '        'Committing to the database, clear value-status-pairs and graphic-status pairs
    '        hs.SaveEventsDevices()

    '        hs.DeviceVSP_ClearAll(reference, True)
    '        hs.DeviceVGP_ClearAll(reference, True)


    '        'Return the reference
    '        Return reference

    '    Catch ex As Exception
    '        Log("Unable to create basic device. Error: " & ex.Message, LogType.Warning)
    '    End Try
    '    Return 0 'if anything fails.
    'End Function

    'Private Function CreateAdvancedDevice(ByVal name As String) As Integer
    '    'Creating BASIC device and getting its device reference
    '    Dim device As Scheduler.Classes.DeviceClass = hs.GetDeviceByRef(CreateBasicDevice(name))
    '    Dim reference As Integer = device.Ref(hs)

    '    'This device type is not Basic, it's Advanced
    '    device.Device_Type_String(hs) = Me.Name & " " & "Advanced"

    '    'Commit to the database
    '    hs.SaveEventsDevices()

    '    'We'll create three controls, a button with the value 0, a slider for values 1 to 9, and yet another button for the value 10

    '    ' =========
    '    ' Value = 0
    '    ' =========
    '    'Status pair
    '    Dim SVpair As New HomeSeerAPI.VSPair(HomeSeerAPI.ePairStatusControl.Both)
    '    SVpair.PairType = HomeSeerAPI.VSVGPairType.SingleValue
    '    SVpair.Value = 0
    '    SVpair.Status = "Value " & SVpair.Value
    '    SVpair.ControlUse = ePairControlUse._Off 'For IFTTT/HStouch support
    '    SVpair.Render = Enums.CAPIControlType.Button
    '    SVpair.IncludeValues = True
    '    hs.DeviceVSP_AddPair(reference, SVpair)

    '    '... and some graphics
    '    Dim VGpair As New HomeSeerAPI.VGPair
    '    VGpair.PairType = HomeSeerAPI.VSVGPairType.SingleValue
    '    VGpair.Set_Value = 0
    '    VGpair.Graphic = "images/checkbox_disabled_on.png"
    '    hs.DeviceVGP_AddPair(reference, VGpair)

    '    ' ============
    '    ' Value 1 to 9
    '    ' ============
    '    'Status pair
    '    SVpair = New HomeSeerAPI.VSPair(HomeSeerAPI.ePairStatusControl.Both)
    '    SVpair.PairType = HomeSeerAPI.VSVGPairType.Range
    '    SVpair.RangeStart = 1
    '    SVpair.RangeEnd = 9
    '    SVpair.RangeStatusPrefix = "Value "
    '    SVpair.ControlUse = ePairControlUse._Dim 'For HStouch support
    '    SVpair.Render = Enums.CAPIControlType.ValuesRangeSlider
    '    SVpair.IncludeValues = True
    '    hs.DeviceVSP_AddPair(reference, SVpair)

    '    '... and some graphics
    '    VGpair = New HomeSeerAPI.VGPair
    '    VGpair.PairType = HomeSeerAPI.VSVGPairType.Range
    '    VGpair.RangeStart = 1
    '    VGpair.RangeEnd = 9
    '    VGpair.Graphic = "images/checkbox_on.png"
    '    hs.DeviceVGP_AddPair(reference, VGpair)

    '    ' ==========
    '    ' Value = 10
    '    ' ==========
    '    'Status pair
    '    SVpair = New HomeSeerAPI.VSPair(HomeSeerAPI.ePairStatusControl.Both)
    '    SVpair.PairType = HomeSeerAPI.VSVGPairType.SingleValue
    '    SVpair.Value = 10
    '    SVpair.Status = "Value " & SVpair.Value
    '    SVpair.ControlUse = ePairControlUse._On 'For IFTTT/HStouch support
    '    SVpair.Render = Enums.CAPIControlType.Button
    '    SVpair.IncludeValues = True
    '    hs.DeviceVSP_AddPair(reference, SVpair)

    '    '... and some graphics
    '    VGpair = New HomeSeerAPI.VGPair
    '    VGpair.PairType = HomeSeerAPI.VSVGPairType.SingleValue
    '    VGpair.Set_Value = 10
    '    VGpair.Graphic = "images/checkbox_hvr.png"
    '    hs.DeviceVGP_AddPair(reference, VGpair)


    '    'return the reference
    '    Return reference
    'End Function

    '''' <summary>
    '''' Creates a root/parent device based on the basic device
    '''' </summary>
    '''' <param name="name"></param>
    '''' <returns></returns>
    '''' <remarks></remarks>
    'Private Function CreateRootDevice(ByVal name As String) As Integer
    '    'Creating BASIC device and getting its device reference
    '    Dim device As Scheduler.Classes.DeviceClass = hs.GetDeviceByRef(CreateBasicDevice(name))

    '    'This device type is not Basic, it's Advanced
    '    device.Device_Type_String(hs) = Me.Name & " " & "Root"

    '    'Setting it as a root device
    '    device.Relationship(hs) = HomeSeerAPI.Enums.eRelationship.Parent_Root

    '    'Committing to the database and return the reference
    '    hs.SaveEventsDevices()
    '    Return device.Ref(hs)
    'End Function

    'Private Function CreateChildDevice(ByVal name As String) As Integer
    '    'Creating BASIC device and getting its device reference
    '    Dim device As Scheduler.Classes.DeviceClass = hs.GetDeviceByRef(CreateBasicDevice(name))

    '    'This device type is not Basic, it's Advanced
    '    device.Device_Type_String(hs) = Me.Name & " " & "Child"

    '    'Setting it as a child device
    '    device.Relationship(hs) = HomeSeerAPI.Enums.eRelationship.Child

    '    'Committing to the database and return the reference
    '    hs.SaveEventsDevices()
    '    Return device.Ref(hs)
    'End Function

    Public Function Capabilities() As Integer Implements IPlugInAPI.Capabilities
        Throw New NotImplementedException()
    End Function

    Private Function IPlugInAPI_AccessLevel() As Integer Implements IPlugInAPI.AccessLevel
        Throw New NotImplementedException()
    End Function

    Public Function SupportsMultipleInstances() As Boolean Implements IPlugInAPI.SupportsMultipleInstances
        Throw New NotImplementedException()
    End Function

    Public Function SupportsMultipleInstancesSingleEXE() As Boolean Implements IPlugInAPI.SupportsMultipleInstancesSingleEXE
        Throw New NotImplementedException()
    End Function

    Public Function SupportsAddDevice() As Boolean Implements IPlugInAPI.SupportsAddDevice
        Throw New NotImplementedException()
    End Function

    Public Function InstanceFriendlyName() As String Implements IPlugInAPI.InstanceFriendlyName
        Throw New NotImplementedException()
    End Function

    Public Function InterfaceStatus() As IPlugInAPI.strInterfaceStatus Implements IPlugInAPI.InterfaceStatus
        Throw New NotImplementedException()
    End Function

    Public Function GenPage(link As String) As String Implements IPlugInAPI.GenPage
        Throw New NotImplementedException()
    End Function

    Public Function PagePut(data As String) As String Implements IPlugInAPI.PagePut
        Throw New NotImplementedException()
    End Function

    Private Sub IPlugInAPI_ShutdownIO() Implements IPlugInAPI.ShutdownIO
        Throw New NotImplementedException()
    End Sub

    Public Function RaisesGenericCallbacks() As Boolean Implements IPlugInAPI.RaisesGenericCallbacks
        Throw New NotImplementedException()
    End Function

    Private Sub IPlugInAPI_SetIOMulti(colSend As List(Of CAPIControl)) Implements IPlugInAPI.SetIOMulti
        Throw New NotImplementedException()
    End Sub

    Private Function IPlugInAPI_InitIO(port As String) As String Implements IPlugInAPI.InitIO
        Throw New NotImplementedException()
    End Function

    Public Function PollDevice(dvref As Integer) As IPlugInAPI.PollResultInfo Implements IPlugInAPI.PollDevice
        Throw New NotImplementedException()
    End Function

    Public Function SupportsConfigDevice() As Boolean Implements IPlugInAPI.SupportsConfigDevice
        Throw New NotImplementedException()
    End Function

    Public Function SupportsConfigDeviceAll() As Boolean Implements IPlugInAPI.SupportsConfigDeviceAll
        Throw New NotImplementedException()
    End Function

    Private Function IPlugInAPI_ConfigDevicePost(ref As Integer, data As String, user As String, userRights As Integer) As Enums.ConfigDevicePostReturn Implements IPlugInAPI.ConfigDevicePost
        Throw New NotImplementedException()
    End Function

    Private Function IPlugInAPI_ConfigDevice(ref As Integer, user As String, userRights As Integer, newDevice As Boolean) As String Implements IPlugInAPI.ConfigDevice
        Throw New NotImplementedException()
    End Function

    Public Function Search(SearchString As String, RegEx As Boolean) As SearchReturn() Implements IPlugInAPI.Search
        Throw New NotImplementedException()
    End Function

    Public Function PluginFunction(procName As String, parms() As Object) As Object Implements IPlugInAPI.PluginFunction
        Throw New NotImplementedException()
    End Function

    Public Function PluginPropertyGet(procName As String, parms() As Object) As Object Implements IPlugInAPI.PluginPropertyGet
        Throw New NotImplementedException()
    End Function

    Public Sub PluginPropertySet(procName As String, value As Object) Implements IPlugInAPI.PluginPropertySet
        Throw New NotImplementedException()
    End Sub

    Public Sub SpeakIn(device As Integer, txt As String, w As Boolean, host As String) Implements IPlugInAPI.SpeakIn
        Throw New NotImplementedException()
    End Sub

    Private Function IPlugInAPI_ActionCount() As Integer Implements IPlugInAPI.ActionCount
        Throw New NotImplementedException()
    End Function

    Private Function IPlugInAPI_ActionConfigured(ActInfo As IPlugInAPI.strTrigActInfo) As Boolean Implements IPlugInAPI.ActionConfigured
        Throw New NotImplementedException()
    End Function

    Private Function IPlugInAPI_ActionBuildUI(sUnique As String, ActInfo As IPlugInAPI.strTrigActInfo) As String Implements IPlugInAPI.ActionBuildUI
        Throw New NotImplementedException()
    End Function

    Private Function IPlugInAPI_ActionProcessPostUI(PostData As NameValueCollection, TrigInfoIN As IPlugInAPI.strTrigActInfo) As IPlugInAPI.strMultiReturn Implements IPlugInAPI.ActionProcessPostUI
        Throw New NotImplementedException()
    End Function

    Private Function IPlugInAPI_ActionFormatUI(ActInfo As IPlugInAPI.strTrigActInfo) As String Implements IPlugInAPI.ActionFormatUI
        Throw New NotImplementedException()
    End Function

    Public Function ActionReferencesDevice(ActInfo As IPlugInAPI.strTrigActInfo, dvRef As Integer) As Boolean Implements IPlugInAPI.ActionReferencesDevice
        Throw New NotImplementedException()
    End Function

    Private Function IPlugInAPI_HandleAction(ActInfo As IPlugInAPI.strTrigActInfo) As Boolean Implements IPlugInAPI.HandleAction
        Throw New NotImplementedException()
    End Function

    Private Function IPlugInAPI_TriggerBuildUI(sUnique As String, TrigInfo As IPlugInAPI.strTrigActInfo) As String Implements IPlugInAPI.TriggerBuildUI
        Throw New NotImplementedException()
    End Function

    Private Function IPlugInAPI_TriggerProcessPostUI(PostData As NameValueCollection, TrigInfoIN As IPlugInAPI.strTrigActInfo) As IPlugInAPI.strMultiReturn Implements IPlugInAPI.TriggerProcessPostUI
        Throw New NotImplementedException()
    End Function

    Private Function IPlugInAPI_TriggerFormatUI(TrigInfo As IPlugInAPI.strTrigActInfo) As String Implements IPlugInAPI.TriggerFormatUI
        Throw New NotImplementedException()
    End Function

    Private Function IPlugInAPI_TriggerTrue(TrigInfo As IPlugInAPI.strTrigActInfo) As Boolean Implements IPlugInAPI.TriggerTrue
        Throw New NotImplementedException()
    End Function

    Public Function TriggerReferencesDevice(TrigInfo As IPlugInAPI.strTrigActInfo, dvRef As Integer) As Boolean Implements IPlugInAPI.TriggerReferencesDevice
        Throw New NotImplementedException()
    End Function

    Private Function IPlugInAPI_GetPagePlugin(page As String, user As String, userRights As Integer, queryString As String) As String Implements IPlugInAPI.GetPagePlugin
        Throw New NotImplementedException()
    End Function

    Private Function IPlugInAPI_PostBackProc(page As String, data As String, user As String, userRights As Integer) As String Implements IPlugInAPI.PostBackProc
        Throw New NotImplementedException()
    End Function
#End Region




End Class
