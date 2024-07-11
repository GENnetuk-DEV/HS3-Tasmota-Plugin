Imports System.Text
Imports System.Web
Imports Scheduler
Imports HomeSeerAPI

Public Class web_config
    Inherits clsPageBuilder
    Dim TimerEnabled As Boolean

    Public Sub New(ByVal pagename As String)
        MyBase.New(pagename)
    End Sub

    Public Overrides Function postBackProc(page As String, data As String, user As String, userRights As Integer) As String
        Dim parts As Collections.Specialized.NameValueCollection = HttpUtility.ParseQueryString(data)

        Debug(2, "Web", "Processing Part [" + parts("id").ToString + "]")

        'Gets the control that caused the postback and handles accordingly
        Select Case parts("id")

            Case "oTextbox1"
                'This gets the value that was entered in the specified textbox
                Dim message As String = parts("Textbox1")

                '... posts it to the page
                PostMessage("Cmessage", message)

                '... and rebuilds the viewed textbox to contain the message
                BuildTextBox("Textbox1", True, message)

            Case "oDebugLevel"
                Dim DebugLevel As String = parts("DebugLevel")
                If IsNumeric(DebugLevel) Then
                    GlobalVariables.DebugLevel = CInt(DebugLevel)
                End If

                PostMessage("Dmessage", "Debug Level Now " + DebugLevel)
                Debug(5, "Web", "Changed DebugLevel to " + DebugLevel)
                AddWarning(0, "DebugLevel Changed to " + DebugLevel)

                PostMessage("Dmessage", "DebugLogging : " & DebugLevel.ToString)

            Case "RefreshButtonStatus"
                'This button navigates to the sample status page.
                'Me.pageCommands.Add("newpage", plugin.Name & "Status")
                Debug(3, "Web", "Processed Status Refresh Button")
                Me.pageCommands.Add("Refresh", "true")
            Case "RefreshButtonConfig"
                'This button navigates to the sample status page.
                'Me.pageCommands.Add("newpage", plugin.Name & "Status")
                Debug(3, "Web", "Processed Config Refresh Button")
                Me.pageCommands.Add("Refresh", "true")
            Case "RefreshButtonDebug"
                'This button navigates to the sample status page.
                'Me.pageCommands.Add("newpage", plugin.Name & "Status")
                Debug(3, "Web", "Processed Debug Refresh Button")
                Me.divToUpdate.Add("debugq", DebugQTable())
                Me.divToUpdate.Add("statsdiv", Statistics)
            Case "ReloadButton"
                Debug(3, "Web", "Reload Initiated")
                'PostMessage("Cmessage", "Reload not yet implemented")
                AddWarning(0, "Reload Initiated")
                GlobalVariables.EnableQueue = False
                Threading.Thread.Sleep(5000)    '5 seconds
                plugin.ReadDevices()
                GlobalVariables.EnableQueue = True
                Debug(3, "Web", "Reload Completed")
                AddWarning(0, "Reload Completed")
                PostMessage("Smessage", "Reload Completed")

            'Configs
            '
            Case "oMQTTBroker"
                plugin.Settings.BrokerIPAddress = parts("MQTTBroker")
                Debug(4, "WEB", "Changed MQTTBroker to [" + parts("MQTTBroker") + "]")
            Case "oMQTTUsername"
                plugin.Settings.BrokerUsername = parts("MQTTUsername")
                Debug(4, "WEB", "Changed MQTTUsername to [" + parts("MQTTUsername") + "]")
            Case "oMQTTPassword"
                plugin.Settings.BrokerPassword = parts("MQTTPassword")
                Debug(4, "WEB", "Changed MQTTPassword to [" + parts("MQTTPassword") + "]")
            Case "oLogFile"
                plugin.Settings.RawLogFile = parts("LogFile")
                Debug(4, "WEB", "Changed Logfile to [" + parts("LogFile") + "]")
            Case "SaveButton"
                Debug(3, "Web", "SAVE Initiated")
                GlobalVariables.EnableQueue = False
                Threading.Thread.Sleep(5000)    '5 seconds
                plugin.Settings.Save()
                Debug(3, "Web", "SAVE Completed")
                PostMessage("Cmessage", "Configuration Saved")

            Case "timer"
                'This stops the timer and clears the message
                If TimerEnabled Then 'this handles the initial timer post that occurs immediately upon enabling the timer.
                    TimerEnabled = False
                Else
                    Me.pageCommands.Add("stoptimer", "")
                    Me.divToUpdate.Add("message", "&nbsp;")
                End If
        End Select

        Return MyBase.postBackProc(page, data, user, userRights)
    End Function

    Public Function GetPagePlugin(ByVal pageName As String, ByVal user As String, ByVal userRights As Integer, ByVal queryString As String) As String
        Dim stb As New StringBuilder
        Dim instancetext As String = ""
        Try
            Me.reset()
            currentPage = Me

            ' handle any queries like mode=something
            Dim parts As Collections.Specialized.NameValueCollection = Nothing
            If queryString <> String.Empty Then parts = HttpUtility.ParseQueryString(queryString)
            If instance <> "" Then instancetext = " - " & instance

            'For some reason, I can't get the sample to add the title. So let's add it here.
            stb.Append("<title>" & plugin.Name & " " & pageName.Replace(plugin.Name, "") & "</title>")

            'Add menus and headers
            stb.Append(hs.GetPageHeader(pageName, plugin.Name & " " & pageName.Replace(plugin.Name, "") & instancetext, "", "", False, False))

            'Adds the div for the plugin page
            stb.Append(clsPageBuilder.DivStart("pluginpage", ""))

            ' a message area for error messages from jquery ajax postback (optional, only needed if using AJAX calls to get data)
            stb.Append(clsPageBuilder.DivStart("errormessage", "class='errormessage'"))
            stb.Append(clsPageBuilder.DivEnd)

            'Configures the timer that all pages apparently has
            Me.RefreshIntervalMilliSeconds = 3000
            stb.Append(Me.AddAjaxHandlerPost("id=timer", pageName)) 'This is so we can control it in postback

            ' specific page starts here
            stb.Append(BuildTabs())

            'Ends the div end tag for the plugin page
            stb.Append(clsPageBuilder.DivEnd)

            ' add the body html to the page
            Me.AddBody(stb.ToString)

            ' return the full page
            Return Me.BuildPage()
        Catch ex As Exception
            'WriteMon("Error", "Building page: " & ex.Message)
            Return "ERROR in GetPagePlugin: " & ex.Message
        End Try
    End Function

    Private Sub PostMessage(ByVal DIVName As String, ByVal message As String)
        'Updates the div
        Me.divToUpdate.Add(DIVName, message)
        'Starts the pages built-in timer
        Me.pageCommands.Add("starttimer", "")

        '... and updates the local variable so we can easily check if the timer is running
        TimerEnabled = True
    End Sub

    Public Function BuildTabs() As String
        Dim stb As New StringBuilder
        Dim tabs As clsJQuery.jqTabs = New clsJQuery.jqTabs("oTabs", Me.PageName)
        Dim tab As New clsJQuery.Tab

        tabs.postOnTabClick = True
        tab.tabTitle = "Status"
        tab.tabDIVID = "oTabStatus"
        tab.tabContent = "<div id='TabConfig_div'>" & BuildContent("Status") & "</div>"
        tabs.tabs.Add(tab)

        tab = New clsJQuery.Tab
        tab.tabTitle = "Config"
        tab.tabDIVID = "oTabConfig"
        tab.tabContent = "<div id='TabRelease_div'>" & BuildContent("Config") & "</div>"
        tabs.tabs.Add(tab)

        tab = New clsJQuery.Tab
        tab.tabTitle = "Release"
        tab.tabDIVID = "oTabRelease"
        tab.tabContent = "<div id='TabRelease_div'>" & BuildContent("Release") & "</div>"
        tabs.tabs.Add(tab)

        tab = New clsJQuery.Tab
        tab.tabTitle = "Debug"
        tab.tabDIVID = "oTabDebug"
        tab.tabContent = "<div id='TabDebug_div'>" & BuildContent("Debug") & "</div>"
        tabs.tabs.Add(tab)

        tab = New clsJQuery.Tab
        tab.tabTitle = "Help"
        tab.tabDIVID = "oTabHelp"
        tab.tabContent = "<div id='TabHelp_div'>" & BuildContent("Help") & "</div>"
        tabs.tabs.Add(tab)

        Return tabs.Build
    End Function
    Function BuildContent(PageName As String) As String
        Dim FG As String = ""
        Dim DType As String = ""
        Dim DState As String = ""
        Dim stb As New StringBuilder
        Dim Tablewidth As Int16 = 950
        Dim Levelname As String = ""

        Select Case PageName
            Case "Status"
                stb.Append("<h2>" + GlobalVariables.ModuleName + " Plug-in</h2>")
                stb.Append("<table cellpadding='0' cellspacing='0' width='" + Tablewidth.ToString + "'>")
                stb.Append("<tr class='tablerowodd'><td>Version</td><td>" + Version + " " + Release + "</td></tr>")
                stb.Append("<tr class='tablerowodd'><td>MQTTBroker Connected</td><td>" + BoolToYesNo(GlobalVariables.MQTTConnected) + "</td></tr>")
                stb.Append("<tr class='tablerowodd'><td>Queue Processing</td><td>" + BoolToYesNo(GlobalVariables.EnableQueue) + "</td></tr>")
                stb.Append("<tr class='tablerowodd'><td>Devices</td><td>" + GlobalVariables.TasmotaDevices.Count.ToString + "</td></tr>")
                stb.Append("<tr class='tablerowodd'><td>Logging to File</td><td>" + BoolToYesNo(GlobalVariables.LogFile) + "</td></tr>")
                stb.Append("<tr><td colspan='2' align='center' style='color:#FF0000; font-size:12pt;'><strong><div id='Smessage'>&nbsp;</div></strong></td></tr>")
                stb.Append("</table>")
                stb.Append("<h2>Devices</h2>")
                stb.Append("<table style='border: 1px solid grey;' cellpadding='4' cellspacing='4' width='" + Tablewidth.ToString + "'>")
                stb.Append("<tr style='color:#FFFFFF; background-color:#2E64FE'><td><strong>ID</strong></td><td><strong>HS Device</strong></td><td><strong>Type</strong></td><td><strong>State</strong></td><td><strong>Device Name</strong></td><td><strong>Target</strong></td><td><strong>Signal</strong></td><td><Strong>Last Seen</strong></td><td><strong>Active</strong></td><td>Uptime</td></tr>")
                For Each TasDevice In GlobalVariables.TasmotaDevices
                    If TasDevice.Active Then FG = "#000000" Else FG = "#6E6E6E"
                    Select Case TasDevice.Type
                        Case 0 : DType = "Binary Power"
                            If hs.IsON(TasDevice.DeviceID) = True Then DState = "ON" Else DState = "OFF"
                        Case 1 : DType = "Sensor Value"
                            DState = hs.DeviceValueEx(TasDevice.DeviceID).ToString
                        Case Else
                            DType = "Other"
                    End Select
                    stb.Append("<tr style='color:" + FG + "'><td><a href='/deviceutility?ref=" + TasDevice.DeviceID.ToString + "&edit=1' target='_self'>" + TasDevice.DeviceID.ToString + "</a></td><td>" + TasDevice.HSDeviceName + "</td><td>" + DType + "</td><td>" + DState + "</td><td>" + TasDevice.DeviceName + "</td><td>" + TasDevice.DeviceTarget + "</td><td>" + TasDevice.RSSI.ToString + "%</td><td>" + TasDevice.LastSeen.ToString + "</td><td>" + BoolToYesNo(TasDevice.Active.ToString) + "</td><td>" + TasDevice.Uptime.ToString + "</td></tr>")
                Next
                stb.Append("</table>")
                If GlobalVariables.Warnings.Count > 0 Then
                    stb.Append("<h2>Messages</h2>")
                    stb.Append("<table style='border: 1px solid grey;' cellpadding='4' cellspacing='4' width='" + Tablewidth.ToString + "'>")
                    stb.Append("<tr style='color:#FFFFFF; background-color:#2E64FE'><td><strong>Time</strong></td><td>Age (mins)</td><td><strong>Level</strong></td><td><strong>Warning</strong></td></tr>")
                    For Each W In GlobalVariables.Warnings '0 = Info, 1 = Notice, 2 = Warning, 3 = Error, 4 = Critical
                        Select Case W.Level
                            Case 0 : Levelname = "Info"
                            Case 1 : Levelname = "Notice"
                            Case 2 : Levelname = "Warning"
                            Case 3 : Levelname = "Error"
                            Case 4 : Levelname = "Critical"
                            Case Else
                                Levelname = "Unknown"
                        End Select
                        stb.Append("<tr style='color:#000000'><td>" + W.TimeStamp.ToString + "</td><td>" + DateDiff(DateInterval.Minute, W.TimeStamp, DateTime.Now).ToString + "</td><td>" + Levelname + "</td><td>" + W.Warning.ToString + "</td></tr>")
                    Next
                    stb.Append("</table>")
                End If
                stb.Append("<p>" + BuildButton("RefreshButtonStatus", False) + "</p>")
                stb.Append("<p>" + BuildButton("ReloadButton", False) + "</p>")
                stb.Append("<br /><br />")
                stb.Append("IMPORTANT: MQTT Topics are case sensitive. Sonoff4 is *NOT* the same as sonoff4 and likewise POWER1 is not the same as power1.")
                stb.Append("<br /><br />")
                stb.Append("<p>Thanks to</p>")
                stb.Append("<ul><li>Theo Arends, for <a href='https://github.com/arendst/Sonoff-Tasmota/wiki' target='_blank'>Tasmota</a> Firmware</li>")
                stb.Append("<li>Hamidreza Mohaqeq, for the M2MQTT Library</li>")
                stb.Append("<li>Steve Quinlan, for Help, Feedback and Alpha Testing</li></ul>")
            Case "Config"
                stb.Append("<h2>" + GlobalVariables.ModuleName + " Plug-in</h2>")
                stb.Append("<table cellpadding='0' cellspacing='0' width='" + Tablewidth.ToString + "'>")
                stb.Append("<tr class='tablerowodd'><td>Version</td><td>" + Version + " " + Release + "</td></tr>")

                stb.Append("<tr class='tablerowodd'><td>MQTTBroker Connected</td><td>" + BoolToYesNo(GlobalVariables.MQTTConnected) + "</td></tr>")
                stb.Append("<tr class='tablerowodd'><td>Queue Processing</td><td>" + BoolToYesNo(GlobalVariables.EnableQueue) + "</td></tr>")
                stb.Append("<tr class='tablerowodd'><td>Devices</td><td>" + GlobalVariables.TasmotaDevices.Count.ToString + "</td></tr>")
                stb.Append("<tr class='tablerowodd'><td>Logging to File</td><td>" + BoolToYesNo(GlobalVariables.LogFile) + "</td></tr>")
                stb.Append("<tr class='tablerowodd'><td>CAPI DoUpdate</td><td>" + BoolToYesNo(GlobalVariables.DoUpdate) + "</td></tr>")
                stb.Append("<tr><td colspan='2' align='center' style='color:#FF0000; font-size:12pt;'><strong><div id='Cmessage'>&nbsp;</div></strong></td></tr>")
                stb.Append("<tr class='tablerowodd'><td>MQTT Broker</td><td>" + BuildTextBox("MQTTBroker", False, plugin.Settings.BrokerIPAddress) + "</td></tr>")
                stb.Append("<tr class='tablerowodd'><td>MQTT Username</td><td>" + BuildTextBox("MQTTUsername", False, plugin.Settings.BrokerUsername) + "</td></tr>")
                stb.Append("<tr class='tablerowodd'><td>MQTT Password</td><td>" + BuildTextBox("MQTTPassword", False, plugin.Settings.BrokerPassword) + "</td></tr>")
                stb.Append("<tr class='tablerowodd'><td>Log File</td><td>" + BuildTextBox("LogFile", False, plugin.Settings.RawLogFile) + "</td></tr>")
                stb.Append("<tr><td colspan='3'>&nbsp;</td></tr>")
                stb.Append("<tr><td colspan='2'>" + BuildButton("SaveButton", False) + "</td></tr>")
                stb.Append("<tr><td colspan='2'>" + BuildButton("RefreshButtonConfig", False) + "</td></tr>")
                stb.Append("</table>")

            Case "Release"
                stb.Append("<table cellpadding='0' cellspacing='0' width='" + Tablewidth.ToString + "'>")
                stb.Append("<tr class='tablerowodd'><td><h2>Latest Version & Release Information</td></tr>")
                stb.Append("<tr><td>Latest Version <b>" + GlobalVariables.LatestVersion + "</b> Currently Running Version <b>" + Version + "</b></td></tr>")
                stb.Append("<tr><td><h2>Release Notes</h2>&nbsp;" + GlobalVariables.LatestVersion + GlobalVariables.ReleaseNotes + "</td></tr>")
                stb.Append("</table>")
                stb.Append("<p>To download the plug-in visit the <a href='https://www.gensupport.net/downloads/download/13-other/42-homeseer-plug-in-for-sonoff-tasmota.html' target='_blank'>DOWNLOAD SITE</a></p>")
            Case "Debug"
                stb.Append("<table border='0' cellpadding='0' cellspacing='0' width='" + Tablewidth.ToString + "'>")
                stb.Append("<tr><td colspan='3' align='center' style='color:#FF0000; font-size:12pt;'><strong><div id='Dmessage'>&nbsp;</div></strong></td></tr>")
                stb.Append("<tr class='tablerowodd'><td>Debug Level</td><td>" + GlobalVariables.DebugLevel.ToString + "</td><td rowspan='6' style='vertical-align: top;'><div id='statsdiv'>" + Statistics + "</div></td></tr>")
                stb.Append("<tr class='tablerowodd'><td>Log File</td><td>" + GlobalVariables.RawLogFileName.ToString + " (" + GlobalVariables.LogFile.ToString + ")</td</tr>")
                stb.Append("<tr class='tablerowodd'><td>Queue</td><td>" + GlobalVariables.MQTTQ.Count().ToString + "</td></tr>")
                stb.Append("<tr class='tablerowodd'><td>Buffer</td><td>" + DebugQ.Count().ToString + "</td></tr>")
                stb.Append("<tr class='tablerowodd'><td>Debug Logging</td><td>" & BuildDropList("DebugLevel") & "</td></tr>")
                stb.Append("<tr class='tablerowodd'><td>Commands</td><td>" & BuildButton("RefreshButtonDebug", False) & "</td></tr>")
                stb.Append("</table>")
                stb.Append(DebugQTable())
            Case "Help"
                stb.Append("<table cellpadding='0' cellspacing='0' width='" + Tablewidth.ToString + "'>")
                stb.Append("<tr class='tablerowodd'><td><h2>Sonoff</h2>")
                stb.Append("<p>ITEAD from Shenzhen China have been around For a While now And their Sonoff range Of control devices are Not only cheap but actually pretty good. When you look at the Sonoff Basic, which Is essentially a power supply, ESP8266 And a relay Then you have a neat wifi switch In a small package. If you actually add up the cost Of the components its hard To make one For the price Of these units but that's the china effect I guess. Anyway, since the 'basic' ITEAD now have a comprehensive range of Sonoff devices, from the Basic, the Dual and the 4 Channel to the elegant touch wall switches and we'd be crazy not to want to hook these up to our HA system.</p>")
                stb.Append("<p>Out of the box the Sonoff device comes with custom firmware that requires an 'app', Internet access and a service that's hosted in China. Whilst this is ok for some, being able to link these directly to our Homeseer is a preferred option. I have now received, flashed and integrated every Sonoff variant and all work just fine which is nice.</p>")
                stb.Append("</td></tr><tr><td><h2>Tasmota</h2>")
                stb.Append("<p>Developed by Theo Arends, the Tasmota firmware runs on the ESP8266 And connects your Sonoff devices to MQTT. The firmware Is GPL And full source Is available for those (Like me) who want to tinker with it to support other devices quickly and easily such as NodeMCU. Flashing your Sonoff device depends on which device but most can be flashed 'over the air' using SonOTA but others require a direct connection to the conveniently placed programming connections. Personally, even though I've used SonOTA, I still prefer the hard wired approach. I won't go over the flashing process here because there are enough guides on the Web covering this.</p>")
                stb.Append("</td></tr><tr><td><h2>MQTT</h2>")
                stb.Append("<p>Back in 1999 IBM came up with MQ Telemetry Transport Protocol to allow for lightweight communication between devices such as sensors in a queue-able and reliable manner. Since then its been developing and is now an accepted standard at version 3.1.1. MQTT is a service provided by a Broker such as Mosquitto which is open source, multi-platform and very stable but there are many more and the choice is very much yours. You'll need to install and test a MQTT broker on your local network in order to make any of this work.</p>")
                stb.Append("<p>The premise of MQTT is simple; There are publishers (devices that send information) and subscribers (devices that listen for information). Devices can be both publishers and subscribers and this is often the way it works out.</p>")
                stb.Append("</td></tr><tr><td><h2>Considerations</h2>")
                stb.Append("<p>MQTT like many things is CaSe SeNsItIve which means that <b>Sonoff</b> is not the same as <b>sonoff</b> and <b>POWER1</b> is not the same as <b>power1</b> or <b>Power1</b>. If you get this wrong it just won't work so check your device names carefully. The links below for device creation also gives a table of output names at the time of writing but the general rule is that outputs are named POWER if there's a single output or POWER1, POWER2 etc if there's more than one.</p>")
                stb.Append("<p>Because of the way MQTT works its entirely possible that when the plug-in loads it will initially receive queued publishes from the broker. This means that you may see historic data from devices that may no longer be on the network - don't worry the plug-in will soon update the status correctly within the first minute or two.")

                stb.Append("</td></tr><tr><td><h2>Links</h2>")
                stb.Append("<ul><li>General Information is available <a href='https://www.gen.net.uk/sonoff-homeseer' target='_blank'>Here</a></li>")
                stb.Append("<li>Device Creation is available <a href='https://www.gen.net.uk/hs3-creating-devices' target='_blank'>Here</a></li>")
                stb.Append("<li>The BugTracker for Reporting Issues is available <a href='https://mantis.gen.net.uk/project_page.php?project_id=24' target='_blank'>Here</a></li></ul>")
                stb.Append("<tr class='tablerowodd'><td>When reporting bugs and issues please include a full description of the issue, a logfile and any screenshots etc that will help us to track down the issue and resolve it.</td></tr>")
                stb.Append("<tr class='tablerowodd'><td></td></tr>")
                stb.Append("</table>")
            Case Else
                stb.Append("<p>There is no content for this tab at the moment</p>")
        End Select
        stb.Append("<p>&copy; 2018 - Developed by Richard Taylor (109). For Support use the BugTracker in the Help Tab.</p>")

        Return stb.ToString
    End Function

    Public Function DebugQTable()
        Dim stb As New StringBuilder
        Dim DebugArray As Array = DebugQ.ToArray
        stb.Append("<div id='debugq'><table cellstacing='0' cellpadding='0' style='table-layout:fixed; padding: 0px; border-spacing: 0px; border-collapse: separate; border: 0px; width:950px'>")
        For i As Integer = UBound(DebugArray) To 0 Step -1
            stb.Append(DebugArray(i))
        Next
        stb.Append("</table></div>")
        Return stb.ToString
    End Function
    Public Function BuildTextBox(ByVal Name As String, Optional ByVal Rebuilding As Boolean = False, Optional ByVal Text As String = "") As String
        Dim textBox As New clsJQuery.jqTextBox(Name, "", Text, Me.PageName, 20, False)
        Dim ret As String = ""
        textBox.id = "o" & Name
        If Rebuilding Then
            ret = textBox.Build
            Me.divToUpdate.Add(Name & "_div", ret)
        Else
            ret = "<div style='float: left;'  id='" & Name & "_div'>" & textBox.Build & "</div>"
        End If

        Return ret
    End Function

    Function BuildButton(ByVal Name As String, Optional ByVal Rebuilding As Boolean = False) As String
        Dim button As New clsJQuery.jqButton(Name, "", Me.PageName, False)
        Dim buttonText As String = "Submit"
        Dim ret As String = String.Empty

        'Handles the text for different buttons, based on the button name
        Select Case Name
            Case "RefreshButtonStatus"
                buttonText = "Refresh"
                button.submitForm = False
            Case "RefreshButtonConfig"
                buttonText = "Refresh"
                button.submitForm = False
            Case "RefreshButtonDebug"
                buttonText = "Refresh"
                button.submitForm = False
            Case "ReloadButton"
                buttonText = "Reload"
                button.submitForm = False
            Case "SaveButton"
                buttonText = "Save"
                button.submitForm = False
        End Select

        'button.id = "o" & Name
        button.id = Name
        button.label = buttonText

        ret = button.Build

        If Rebuilding Then
            Me.divToUpdate.Add(Name & "_div", ret)
        Else
            ret = "<div style='float: left;' id='" & Name & "_div'>" & ret & "</div>"
        End If
        Return ret
    End Function

    Public Function BuildDropList(ByVal Name As String, Optional ByVal Rebuilding As Boolean = False) As String
        Dim ddl As New clsJQuery.jqDropList(Name, Me.PageName, False)
        ddl.id = "o" & Name
        ddl.autoPostBack = True

        Select Case Name
            Case "DebugLevel"
                ddl.AddItem("Normal", "5", True)
                ddl.AddItem("Increased", "4", False)
                ddl.AddItem("Diagnostic 1", "3", False)
                ddl.AddItem("Diagnostic 2", "2", False)
                ddl.AddItem("Low Level", "1", False)
                ddl.AddItem("Everything", "0", False)
        End Select

        Dim ret As String
        If Rebuilding Then
            ret = ddl.Build
            Me.divToUpdate.Add(Name & "_div", ret)
        Else
            ret = "<div style='float: left;'  id='" & Name & "_div'>" & ddl.Build & "</div>"
        End If
        Return ret
    End Function

    Public Function BuildCheckbox(ByVal Name As String, Optional ByVal Rebuilding As Boolean = False, Optional ByVal label As String = "") As String
        Dim checkbox As New clsJQuery.jqCheckBox(Name, label, Me.PageName, True, False)
        'checkbox.id = "o" & Name
        checkbox.id = Name

        Select Case Name

            Case "CheckboxDebugLogging"
                If GlobalVariables.DebugLevel = 0 Then
                    checkbox.checked = True
                Else
                    checkbox.checked = False
                End If
        End Select

        Dim ret As String = String.Empty
        If Rebuilding Then
            ret = checkbox.Build
            Me.divToUpdate.Add(Name & "_div", ret)
        Else
            ret = "<div style='float: left;'  id='" & Name & "_div'>" & checkbox.Build & "</div>"
        End If
        Return ret
    End Function

End Class

