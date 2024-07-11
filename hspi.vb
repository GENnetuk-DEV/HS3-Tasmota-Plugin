Imports System
Imports Scheduler
Imports HomeSeerAPI
Imports HSCF.Communication.Scs.Communication.EndPoints.Tcp
Imports HSCF.Communication.ScsServices.Client
Imports HSCF.Communication.ScsServices.Service
Imports System.Reflection

Public Class HSPI
    Implements IPlugInAPI        ' this API is required for ALL plugins
    'Implements IThermostatAPI   ' add this API if this plugin supports thermostats

    Public Function PluginFunction(ByVal proc As String, ByVal parms() As Object) As Object Implements IPlugInAPI.PluginFunction
        Try
            Dim [type] As Type = Me.GetType
            Dim methodInfo As MethodInfo = [type].GetMethod(proc)
            If methodInfo Is Nothing Then
                Debug(9, "PlugInFunction", "Method " & proc & " does not exist in this plugin.")
                Return Nothing
            End If
            Return (methodInfo.Invoke(Me, parms))
        Catch ex As Exception
            Debug(9, "PlugInFunction", "Error in PluginProc: " & ex.Message)
        End Try

        Return Nothing
    End Function

    Public Function PluginPropertyGet(ByVal proc As String, parms() As Object) As Object Implements IPlugInAPI.PluginPropertyGet
        Try
            Dim [type] As Type = Me.GetType
            Dim propertyInfo As PropertyInfo = [type].GetProperty(proc)
            If propertyInfo Is Nothing Then
                Debug(9, System.Reflection.MethodBase.GetCurrentMethod().Name, "Property " & proc & " does not exist in this plugin ")
                Return Nothing
            End If
            Return propertyInfo.GetValue(Me, parms)
        Catch ex As Exception
            Debug(9, System.Reflection.MethodBase.GetCurrentMethod().Name, "Error in PluginPropertyGet: " & ex.Message)
        End Try

        Return Nothing
    End Function

    Public Sub PluginPropertySet(ByVal proc As String, value As Object) Implements IPlugInAPI.PluginPropertySet
        Try
            Dim [type] As Type = Me.GetType
            Dim propertyInfo As PropertyInfo = [type].GetProperty(proc)
            If propertyInfo Is Nothing Then
                Debug(9, System.Reflection.MethodBase.GetCurrentMethod().Name, "Property " & proc & " does not exist in this plugin.")
            End If
            propertyInfo.SetValue(Me, value, Nothing)
        Catch ex As Exception
            Debug(9, System.Reflection.MethodBase.GetCurrentMethod().Name, "Error in PluginPropertySet: " & ex.Message)
        End Try
    End Sub

    Public ReadOnly Property Name As String Implements HomeSeerAPI.IPlugInAPI.Name
        Get
            Return plugin.Name
        End Get
    End Property

    Public ReadOnly Property HSCOMPort As Boolean Implements HomeSeerAPI.IPlugInAPI.HSCOMPort
        Get
            Return False
        End Get
    End Property

    Public Function Capabilities() As Integer Implements HomeSeerAPI.IPlugInAPI.Capabilities
        Return HomeSeerAPI.Enums.eCapabilities.CA_IO
    End Function

    Public Function AccessLevel() As Integer Implements HomeSeerAPI.IPlugInAPI.AccessLevel
        Return plugin.AccessLevel
    End Function

    Public Function InterfaceStatus() As HomeSeerAPI.IPlugInAPI.strInterfaceStatus Implements HomeSeerAPI.IPlugInAPI.InterfaceStatus
        Dim es As New IPlugInAPI.strInterfaceStatus
        es.intStatus = IPlugInAPI.enumInterfaceStatus.OK
        Return es
    End Function

    Public Function SupportsMultipleInstances() As Boolean Implements HomeSeerAPI.IPlugInAPI.SupportsMultipleInstances
        Return False
    End Function

    Public Function SupportsMultipleInstancesSingleEXE() As Boolean Implements HomeSeerAPI.IPlugInAPI.SupportsMultipleInstancesSingleEXE
        Return False
    End Function

    Public Function InstanceFriendlyName() As String Implements HomeSeerAPI.IPlugInAPI.InstanceFriendlyName
        Return ""
    End Function

    Public Function InitIO(ByVal port As String) As String Implements HomeSeerAPI.IPlugInAPI.InitIO
        Return plugin.InitIO(port)
    End Function

    Public Function RaisesGenericCallbacks() As Boolean Implements HomeSeerAPI.IPlugInAPI.RaisesGenericCallbacks
        Return True
    End Function

    Public Sub SetIOMulti(colSend As System.Collections.Generic.List(Of HomeSeerAPI.CAPI.CAPIControl)) Implements HomeSeerAPI.IPlugInAPI.SetIOMulti
        plugin.SetIOMulti(colSend)
    End Sub

    Public Sub ShutdownIO() Implements HomeSeerAPI.IPlugInAPI.ShutdownIO
        plugin.ShutdownIO()
    End Sub

    Public Sub HSEvent(ByVal EventType As Enums.HSEvent, ByVal parms() As Object) Implements HomeSeerAPI.IPlugInAPI.HSEvent
        Dim dAddress As String = ""
        Dim dValue As Double = 0
        Dim dRef As Integer = 0
        Dim dName As String = ""
        Dim dLocation As String = ""
        Dim dLocation2 As String = ""
        Dim dString As String = ""
        Dim dIsOn As Boolean = False
        Dim mqttString As String = ""
        Dim mqttPayload As String = ""
        Dim d As Scheduler.Classes.DeviceClass

        If Not GlobalVariables.IsShuttingDown Then
            'Debug("HSPI", "HSEVENT:" + EventType.ToString)
            GlobalVariables.StatsHSEvents = GlobalVariables.StatsHSEvents + 1

            Select Case EventType
                Case Enums.HSEvent.STRING_CHANGE
                    dAddress = parms(1)
                    dString = parms(2)
                    dRef = parms(3)
                'Debug(2, "HSEvent", "Device Ref " + dRef.ToString + "(" + dAddress.ToString + ") Has changed its String to [" + dString.ToString + "]")
                'If GlobalVariables.MQTTDevices.ContainsKey(dRef) Then
                'Debug(2, "HSPI", "Our target device changed its String to " + dString.ToString)
                'End If
                Case Enums.HSEvent.VALUE_CHANGE
                    dAddress = parms(1)
                    dValue = parms(2)
                    dRef = parms(4)
                    dName = hs.DeviceName(dRef)

                    Dim OurDevice As DeviceInfo = GlobalVariables.TasmotaDevices.Find(Function(p) p.DeviceID = dRef)
                    If Not OurDevice Is Nothing Then
                        Debug(2, "HSPI", "Ignored the previous " + GlobalVariables.IgnoredEvents.ToString + " events")
                        dString = OurDevice.DeviceString
                        GlobalVariables.IgnoredEvents = 0
                        If OurDevice.Type = 0 Then
                            If OurDevice.Active Then
                                dIsOn = hs.IsON(dRef)
                                If dIsOn Then mqttPayload = "1" Else mqttPayload = "0"
                                Debug(3, "HSPI", "Device " + dRef.ToString + " = " + dValue.ToString + " and isOn=" + dIsOn.ToString + " Queued")
                                GlobalVariables.TasmotaDevices.Find(Function(p) p.DeviceID = dRef).Pending = True
                                GlobalVariables.MQTTQ.Enqueue("cmnd|" + dString + "|" + mqttPayload)
                            Else
                                Debug(5, "HSPI", "Device " + dRef.ToString + " is OFFLINE - ignoring command")
                            End If
                        Else
                            Debug(3, "HSPI", "Device " + dRef.ToString + " Is a Sensor - ignoring change")
                        End If

                    Else
                        Debug(0, "HSEvent", "Device Changed, but not ours " + dRef.ToString + "[" + dName + "] (" + dAddress.ToString + ")")
                        GlobalVariables.IgnoredEvents = GlobalVariables.IgnoredEvents + 1
                    End If
            End Select
        End If
    End Sub

    Public Function PollDevice(ByVal dvref As Integer) As IPlugInAPI.PollResultInfo Implements HomeSeerAPI.IPlugInAPI.PollDevice
    End Function

    Public Function GenPage(ByVal link As String) As String Implements HomeSeerAPI.IPlugInAPI.GenPage
        Return Nothing
    End Function

    Public Function PagePut(ByVal data As String) As String Implements HomeSeerAPI.IPlugInAPI.PagePut
        Return Nothing
    End Function

    Public Function GetPagePlugin(ByVal pageName As String, ByVal user As String, ByVal userRights As Integer, ByVal queryString As String) As String Implements HomeSeerAPI.IPlugInAPI.GetPagePlugin
        Return plugin.GetPagePlugin(pageName, user, userRights, queryString)
    End Function

    Public Function PostBackProc(ByVal pageName As String, ByVal data As String, ByVal user As String, ByVal userRights As Integer) As String Implements HomeSeerAPI.IPlugInAPI.PostBackProc
        Return plugin.PostBackProc(pageName, data, user, userRights)
    End Function

    Public Property ActionAdvancedMode As Boolean Implements HomeSeerAPI.IPlugInAPI.ActionAdvancedMode
        Set(ByVal value As Boolean)
            _actionAdvancedMode = value
        End Set
        Get
            Return _actionAdvancedMode
        End Get
    End Property
    Private _actionAdvancedMode As Boolean

    Public Function ActionBuildUI(ByVal sUnique As String, ByVal ActInfo As IPlugInAPI.strTrigActInfo) As String Implements HomeSeerAPI.IPlugInAPI.ActionBuildUI
        Return plugin.ActionBuildUI(sUnique, ActInfo)
    End Function

    Public Function ActionConfigured(ByVal ActInfo As IPlugInAPI.strTrigActInfo) As Boolean Implements HomeSeerAPI.IPlugInAPI.ActionConfigured
        Return plugin.ActionConfigured(ActInfo)
    End Function

    Public Function ActionReferencesDevice(ByVal ActInfo As IPlugInAPI.strTrigActInfo, ByVal dvRef As Integer) As Boolean Implements HomeSeerAPI.IPlugInAPI.ActionReferencesDevice
        Return False

        'The exmample from the documentation doesn't work, so there must be a different way to figure that out.
    End Function

    Public Function ActionFormatUI(ByVal ActInfo As IPlugInAPI.strTrigActInfo) As String Implements HomeSeerAPI.IPlugInAPI.ActionFormatUI
        Return plugin.ActionFormatUI(ActInfo)
    End Function

    Public ReadOnly Property ActionName(ByVal ActionNumber As Integer) As String Implements HomeSeerAPI.IPlugInAPI.ActionName
        Get
            Return plugin.ActionName(ActionNumber)
        End Get
    End Property

    Public Function ActionProcessPostUI(ByVal PostData As Collections.Specialized.NameValueCollection, ByVal TrigInfoIN As IPlugInAPI.strTrigActInfo) As IPlugInAPI.strMultiReturn Implements HomeSeerAPI.IPlugInAPI.ActionProcessPostUI
        Return plugin.ActionProcessPostUI(PostData, TrigInfoIN)
    End Function

    Public Function ActionCount() As Integer Implements HomeSeerAPI.IPlugInAPI.ActionCount
        Return plugin.ActionCount
    End Function

    Public Property Condition(ByVal TrigInfo As HomeSeerAPI.IPlugInAPI.strTrigActInfo) As Boolean Implements HomeSeerAPI.IPlugInAPI.Condition
        Set(ByVal value As Boolean)
            _condition = value
        End Set
        Get
            Return _condition
        End Get
    End Property
    Dim _condition As Boolean
    Public Function HandleAction(ByVal ActInfo As IPlugInAPI.strTrigActInfo) As Boolean Implements HomeSeerAPI.IPlugInAPI.HandleAction
        Return plugin.HandleAction(ActInfo)
    End Function

    Public ReadOnly Property HasConditions(ByVal TriggerNumber As Integer) As Boolean Implements HomeSeerAPI.IPlugInAPI.HasConditions
        Get
            Return plugin.HasConditions(TriggerNumber)
        End Get
    End Property

    Public Function TriggerTrue(ByVal TrigInfo As HomeSeerAPI.IPlugInAPI.strTrigActInfo) As Boolean Implements HomeSeerAPI.IPlugInAPI.TriggerTrue
        Return plugin.TriggerTrue(TrigInfo)
    End Function

    Public ReadOnly Property HasTriggers() As Boolean Implements HomeSeerAPI.IPlugInAPI.HasTriggers
        Get
            Return plugin.HasTriggers
        End Get
    End Property

    Public ReadOnly Property SubTriggerCount(ByVal TriggerNumber As Integer) As Integer Implements HomeSeerAPI.IPlugInAPI.SubTriggerCount
        Get
            Return plugin.SubTriggerCount(TriggerNumber)
        End Get
    End Property

    Public ReadOnly Property SubTriggerName(ByVal TriggerNumber As Integer, ByVal SubTriggerNumber As Integer) As String Implements HomeSeerAPI.IPlugInAPI.SubTriggerName
        Get
            Return plugin.SubTriggerName(TriggerNumber, SubTriggerNumber)
        End Get
    End Property

    Public Function TriggerBuildUI(ByVal sUnique As String, ByVal TrigInfo As HomeSeerAPI.IPlugInAPI.strTrigActInfo) As String Implements HomeSeerAPI.IPlugInAPI.TriggerBuildUI
        Return plugin.TriggerBuildUI(sUnique, TrigInfo)
    End Function

    Public ReadOnly Property TriggerConfigured(ByVal TrigInfo As HomeSeerAPI.IPlugInAPI.strTrigActInfo) As Boolean Implements HomeSeerAPI.IPlugInAPI.TriggerConfigured
        Get
            Return plugin.TriggerConfigured(TrigInfo)
        End Get
    End Property

    Public Function TriggerReferencesDevice(ByVal TrigInfo As HomeSeerAPI.IPlugInAPI.strTrigActInfo, ByVal dvRef As Integer) As Boolean Implements HomeSeerAPI.IPlugInAPI.TriggerReferencesDevice
        Return False
    End Function

    Public Function TriggerFormatUI(ByVal TrigInfo As HomeSeerAPI.IPlugInAPI.strTrigActInfo) As String Implements HomeSeerAPI.IPlugInAPI.TriggerFormatUI
        Return plugin.TriggerFormatUI(TrigInfo)
    End Function

    Public ReadOnly Property TriggerName(ByVal TriggerNumber As Integer) As String Implements HomeSeerAPI.IPlugInAPI.TriggerName
        Get
            Return plugin.TriggerName(TriggerNumber)
        End Get
    End Property

    Public Function TriggerProcessPostUI(ByVal PostData As System.Collections.Specialized.NameValueCollection, ByVal TrigInfoIn As HomeSeerAPI.IPlugInAPI.strTrigActInfo) As HomeSeerAPI.IPlugInAPI.strMultiReturn Implements HomeSeerAPI.IPlugInAPI.TriggerProcessPostUI
        Return plugin.TriggerProcessPostUI(PostData, TrigInfoIn)
    End Function

    Public ReadOnly Property TriggerCount As Integer Implements HomeSeerAPI.IPlugInAPI.TriggerCount
        Get
            Return plugin.TriggerCount
        End Get
    End Property

    Public Function SupportsConfigDevice() As Boolean Implements HomeSeerAPI.IPlugInAPI.SupportsConfigDevice
        Return True
    End Function

    Public Function SupportsConfigDeviceAll() As Boolean Implements HomeSeerAPI.IPlugInAPI.SupportsConfigDeviceAll
        Return False
    End Function

    Public Function SupportsAddDevice() As Boolean Implements HomeSeerAPI.IPlugInAPI.SupportsAddDevice
        Return False
    End Function

    Function ConfigDevicePost(ByVal ref As Integer, ByVal data As String, ByVal user As String, ByVal userRights As Integer) As Enums.ConfigDevicePostReturn Implements IPlugInAPI.ConfigDevicePost
        Return plugin.ConfigDevicePost(ref, data, user, userRights)
    End Function

    Function ConfigDevice(ByVal ref As Integer, ByVal user As String, ByVal userRights As Integer, newDevice As Boolean) As String Implements IPlugInAPI.ConfigDevice
        Return plugin.ConfigDevice(ref, user, userRights, newDevice)
    End Function

    Public Function Search(SearchString As String, RegEx As Boolean) As HomeSeerAPI.SearchReturn() Implements HomeSeerAPI.IPlugInAPI.Search
        Return Nothing
    End Function

    Public Sub SpeakIn(device As Integer, txt As String, w As Boolean, host As String) Implements HomeSeerAPI.IPlugInAPI.SpeakIn

    End Sub

    Public Shared Sub SetDevice(DeviceID As Long, Command As String)

        Dim Capi As HomeSeerAPI.CAPI.CAPIControl
        Capi = hs.CAPIGetSingleControl(DeviceID, True, Command, False, True)
        If Capi IsNot Nothing Then
            Capi.Do_Update = GlobalVariables.DoUpdate
            hs.CAPIControlHandler(Capi)
            'hs.WriteLog(GlobalVariables.ModuleName, "Set Device " + DeviceID.ToString + " to " + Command)
            Debug(4, "HSPI", "Set Device " + DeviceID.ToString + " to " + Command)
        Else
            Debug(4, "HSPI", "Device " + DeviceID.ToString + " cannot be found by CAPI")
            AddWarning(3, "Device " + DeviceID.ToString + " Command " + Command + " cannot be found by CAPI controller")
        End If
    End Sub

    Public Shared Sub SetDeviceValue(DeviceID As Long, Value As Double)
        hs.SetDeviceValueByRef(DeviceID, Value, True)

        'Capi.Do_Update = False
        'hs.CAPIControlHandler(Capi)
        'hs.WriteLog(GlobalVariables.ModuleName, "Set Device " + DeviceID.ToString + " to " + Command)
        Debug(4, "HSPI", "Set Device " + DeviceID.ToString + " to " + Value.ToString)

    End Sub


#If PlugDLL Then
    ' These 2 functions for internal use only
    Public Property HSObj As HomeSeerAPI.IHSApplication Implements HomeSeerAPI.IPlugInAPI.HSObj
        Get
            Return hs
        End Get
        Set(value As HomeSeerAPI.IHSApplication)
            hs = value
        End Set
    End Property

    Public Property CallBackObj As HomeSeerAPI.IAppCallbackAPI Implements HomeSeerAPI.IPlugInAPI.CallBackObj
        Get
            Return callback
        End Get
        Set(value As HomeSeerAPI.IAppCallbackAPI)
            callback = value
        End Set
    End Property
#End If
End Class

