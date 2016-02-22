using System;
using System.Collections.Generic;
using System.Linq;
using ConnectedLivingSpace;
using ShipManifest.APIClients;
using ShipManifest.InternalObjects;
using ShipManifest.Process;
using ShipManifest.Windows;
using UnityEngine;

namespace ShipManifest
{
  [KSPAddon(KSPAddon.Startup.EveryScene, false)]
  // ReSharper disable once InconsistentNaming
  internal class SMAddon : MonoBehaviour, ITransferProcess
  {
    // Object Scope:  Current Unity/KSP Scene.  Object will be destroyed and recreated when scene changes!

    #region Static Properties

    // Game object that keeps us running
    // internal static GameObject SmInstance;

    internal static bool SceneChangeInitDfWrapper;
    
    // current vessel's controller instance
    internal static SMVessel SmVessel;
    internal static ICLSAddon ClsAddon;

    internal static string TextureFolder = "ShipManifest/Textures/";
    internal static string SaveMessage = string.Empty;

    [KSPField(isPersistant = true)] internal static double Elapsed;

    // Resource xfer vars
    // This is still very entrenched.   Need to look at implications for conversion to instanced.
    internal static TransferPump.TypePump ActivePumpType = TransferPump.TypePump.SourceToTarget;

    // Toolbar Integration.
    private static IButton _smButtonBlizzy;
    private static IButton _smSettingsBlizzy;
    private static IButton _smRosterBlizzy;
    private static ApplicationLauncherButton _smButtonStock;
    private static ApplicationLauncherButton _smSettingsStock;
    private static ApplicationLauncherButton _smRosterStock;

    // Repeating error latch
    internal static bool FrameErrTripped;

    // SM UI toggle
    internal static bool ShowUi = true;

    // SMInterface.ITransferProcess properties
    public ITransferProcess Instance
    {
      get { return this; }
    }

    public bool PumpProcessOn
    {
      get { return TransferPump.PumpProcessOn; }
    }

    public bool CrewProcessOn
    {
      get { return SmVessel.TransferCrewObj.CrewXferActive; }
    }

    public ITransferCrew CrewTransferProcess
    {
      get { return SmVessel.TransferCrewObj; }
    }

    public List<ITransferPump> PumpsInProgress
    {
      get { return (from pump in SmVessel.TransferPumps select (ITransferPump) pump).ToList(); }
    }

    #endregion

    #region Event handlers

    private static void DummyHandler()
    {
    }

    // Addon state event handlers
    internal void Awake()
    {
      try
      {
        if (HighLogic.LoadedScene != GameScenes.FLIGHT && HighLogic.LoadedScene != GameScenes.SPACECENTER) return;
        DontDestroyOnLoad(this);
        SMSettings.LoadSettings();

        if (SMSettings.AutoSave)
          InvokeRepeating("RunSave", SMSettings.SaveIntervalSec, SMSettings.SaveIntervalSec);

        if (SMSettings.EnableBlizzyToolbar)
        {
          // Let't try to use Blizzy's toolbar
          if (ActivateBlizzyToolBar()) return;
          // We failed to activate the toolbar, so revert to stock
          GameEvents.onGUIApplicationLauncherReady.Add(OnGuiAppLauncherReady);
          GameEvents.onGUIApplicationLauncherDestroyed.Add(OnGuiAppLauncherDestroyed);
        }
        else
        {
          // Use stock Toolbar
          GameEvents.onGUIApplicationLauncherReady.Add(OnGuiAppLauncherReady);
          GameEvents.onGUIApplicationLauncherDestroyed.Add(OnGuiAppLauncherDestroyed);
        }
      }
      catch (Exception ex)
      {
        Utilities.LogMessage("Error in:  SMAddon.Awake.  Error:  " + ex, "Error", true);
      }
    }

    internal void Start()
    {
      try
      {
        // Reset frame error latch if set
        if (FrameErrTripped)
          FrameErrTripped = false;

        if (WindowRoster.ResetRosterSize)
          WindowRoster.Position.height = SMSettings.UseUnityStyle ? 330 : 350;

        if (HighLogic.LoadedScene == GameScenes.SPACECENTER)
        {
          if (GetClsAddon())
          {
            SMSettings.ClsInstalled = true;
          }
          else
          {
            SMSettings.EnableCls = false;
            SMSettings.ClsInstalled = false;
          }
          // reset any hacked kerbal names in game save from old version of SM/KSP
          if (SMSettings.RenameWithProfession)
            WindowRoster.ResetKerbalNames();

          SMSettings.SaveSettings();
          //RunSave();
        }

        if (HighLogic.LoadedScene != GameScenes.FLIGHT) return;
        // Instantiate Event handlers
        GameEvents.onGameSceneLoadRequested.Add(OnGameSceneLoadRequested);
        GameEvents.onVesselChange.Add(OnVesselChange);
        GameEvents.onVesselWasModified.Add(OnVesselWasModified);
        GameEvents.onVesselChange.Add(OnVesselChange);
        GameEvents.onVesselLoaded.Add(OnVesselLoaded);
        GameEvents.onCrewTransferred.Add(OnCrewTransferred);
        GameEvents.onShowUI.Add(OnShowUi);
        GameEvents.onHideUI.Add(OnHideUi);

        // get the current Vessel data
        SmVessel = SMVessel.GetInstance(FlightGlobals.ActiveVessel);

        // Is CLS installed and enabled?
        if (GetClsAddon())
        {
          SMSettings.ClsInstalled = true;
          SMSettings.SaveSettings();
          UpdateClsSpaces();
        }
        else
        {
          SMSettings.EnableCls = false;
          SMSettings.ClsInstalled = false;
          SMSettings.SaveSettings();
        }

        // Support for DeepFreeze
        //Trigger Update to check and initialize the DeepFreeze Wrapper API
        SceneChangeInitDfWrapper = true;

        // Load sounds for transfers.
        SMSound.LoadSounds();
      }
      catch (Exception ex)
      {
        Utilities.LogMessage("Error in:  SMAddon.Start.  " + ex, "Error", true);
      }
    }

    internal void OnDestroy()
    {
      //Debug.Log("[ShipManifest]:  SmAddon.OnDestroy");
      try
      {
        if (HighLogic.LoadedSceneIsFlight)
          WindowControl.ShowWindow =
            WindowManifest.ShowWindow =
              WindowTransfer.ShowWindow = WindowRoster.ShowWindow = WindowSettings.ShowWindow = false;
        if (HighLogic.LoadedScene == GameScenes.SPACECENTER)
          WindowRoster.ShowWindow = WindowSettings.ShowWindow = false;

        if (SMSettings.Loaded)
          SMSettings.SaveSettings();

        GameEvents.onGameSceneLoadRequested.Remove(OnGameSceneLoadRequested);
        GameEvents.onVesselWasModified.Remove(OnVesselWasModified);
        GameEvents.onVesselChange.Remove(OnVesselChange);
        GameEvents.onVesselWasModified.Remove(OnVesselWasModified);
        GameEvents.onVesselChange.Remove(OnVesselChange);
        GameEvents.onVesselLoaded.Remove(OnVesselLoaded);
        GameEvents.onCrewTransferred.Remove(OnCrewTransferred);
        GameEvents.onHideUI.Remove(OnHideUi);
        GameEvents.onShowUI.Remove(OnShowUi);

        CancelInvoke("RunSave");

        // Handle Toolbars
        if (_smRosterBlizzy == null && _smSettingsBlizzy == null && _smButtonBlizzy == null)
        {
          if (_smButtonStock != null)
          {
            ApplicationLauncher.Instance.RemoveModApplication(_smButtonStock);
            _smButtonStock = null;
          }
          if (_smSettingsStock != null)
          {
            ApplicationLauncher.Instance.RemoveModApplication(_smSettingsStock);
            _smSettingsStock = null;
          }
          if (_smRosterStock != null)
          {
            ApplicationLauncher.Instance.RemoveModApplication(_smRosterStock);
            _smRosterStock = null;
          }
          if (_smButtonStock == null && _smSettingsStock == null && _smRosterStock == null)
          {
            // Remove the stock toolbar button launcher handler
            GameEvents.onGUIApplicationLauncherReady.Remove(OnGuiAppLauncherReady);
          }
        }
        else
        {
          if (_smButtonBlizzy != null)
            _smButtonBlizzy.Destroy();
          if (_smRosterBlizzy != null)
            _smRosterBlizzy.Destroy();
          if (_smSettingsBlizzy != null)
            _smSettingsBlizzy.Destroy();
        }
        //Reset Roster Window data
        WindowRoster.OnCreate = false;
        WindowRoster.SelectedKerbal = null;
        WindowRoster.ToolTip = "";
        //WindowRoster.ShowWindow = false;
      }
      catch (Exception ex)
      {
        Utilities.LogMessage("Error in:  SMAddon.OnDestroy.  " + ex, "Error", true);
      }
    }

    // ReSharper disable once InconsistentNaming
    internal void OnGUI()
    {
      try
      {
        GUI.skin = SMSettings.UseUnityStyle ? null : HighLogic.Skin;

        SMStyle.SetupGuiStyles();
        Display();
        SMToolTips.ShowToolTips();
      }
      catch (Exception ex)
      {
        Utilities.LogMessage("Error in:  SMAddon.OnGUI.  " + ex, "Error", true);
      }
    }

    internal void Update()
    {
      try
      {
        CheckForToolbarTypeToggle();

        if (SceneChangeInitDfWrapper && Time.timeSinceLevelLoad > 3f && !InstalledMods.IsDfApiReady)
        {
          DFWrapper.InitDFWrapper();
          SceneChangeInitDfWrapper = false;
        }

        if (HighLogic.LoadedScene != GameScenes.FLIGHT) return;
        if (FlightGlobals.fetch == null || FlightGlobals.ActiveVessel == null) return;

        //Instantiate the controller for the active vessel.
        //SmVessel = SMVessel.GetInstance(FlightGlobals.ActiveVessel);
        SMHighlighter.Update_Highlighter();

        // Realism Mode Resource transfer operation (real time)
        // PumpActive is flagged in the Resource Controller
        if (TransferPump.PumpProcessOn)
        {
          if ((from pump in SmVessel.TransferPumps where pump.IsPumpOn select pump).Any())
            TransferPump.ProcessActivePumps();
          else
          {
            TransferPump.PumpProcessOn = false;
            SmVessel.TransferPumps.Clear();
          }
        }

        // Realism Mode Crew transfer operation (real time)
        if (SmVessel.TransferCrewObj.CrewXferActive)
          SmVessel.TransferCrewObj.CrewTransferProcess();
        else if (SmVessel.TransferCrewObj.IsStockXfer)
        {
          TransferCrew.RevertCrewTransfer(SmVessel.TransferCrewObj.FromCrewMember, SmVessel.TransferCrewObj.FromPart,
            SmVessel.TransferCrewObj.ToPart);
          SmVessel.TransferCrewObj.CrewTransferBegin(SmVessel.TransferCrewObj.FromCrewMember,
            SmVessel.TransferCrewObj.FromPart, SmVessel.TransferCrewObj.ToPart);
        }

        if (!SMSettings.EnableOnCrewTransferEvent || !TransferCrew.FireSourceXferEvent) return;
        // Now let's deal with third party mod support...
        TransferCrew.FireSourceXferEvent = false;
        GameEvents.onCrewTransferred.Fire(TransferCrew.SourceAction);

        //If a swap, we need to handle that too...
        if (!TransferCrew.FireTargetXferEvent) return;
        TransferCrew.FireTargetXferEvent = false;
        GameEvents.onCrewTransferred.Fire(TransferCrew.TargetAction);
      }
      catch (Exception ex)
      {
        if (!FrameErrTripped)
        {
          Utilities.LogMessage(
            string.Format(" in SMAddon.Update (repeating error).  Error:  {0} \r\n\r\n{1}", ex.Message, ex.StackTrace),
            "Error", true);
          FrameErrTripped = true;
        }
      }
    }

    // save settings on scene changes
    private void OnGameSceneLoadRequested(GameScenes requestedScene)
    {
      //Debug.Log("[ShipManifest]:  SMAddon.OnGameSceneLoadRequested");
      SMSettings.SaveSettings();
      if (InstalledMods.IsDfInstalled)
        SceneChangeInitDfWrapper = true;
    }

    // SM UI toggle handlers
    private void OnShowUi()
    {
      //Debug.Log("[ShipManifest]:  SMAddon.OnShowUI");
      ShowUi = true;
    }

    private void OnHideUi()
    {
      //Debug.Log("[ShipManifest]:  SMAddon.OnHideUI");
      ShowUi = false;
    }

    // Crew Event handlers
    internal void OnCrewTransferred(GameEvents.HostedFromToAction<ProtoCrewMember, Part> action)
    {
      if ((action.host == TransferCrew.SourceAction.host && action.from == TransferCrew.SourceAction.from &&
           action.to == TransferCrew.SourceAction.to)
          ||
          action.host == TransferCrew.TargetAction.host && action.from == TransferCrew.TargetAction.from &&
          action.to == TransferCrew.TargetAction.to)
      {
        // We are performing a mod notification. Ignore the event.
        return;
      }
      if (!SmVessel.TransferCrewObj.CrewXferActive && (!SMSettings.OverrideStockCrewXfer ||
                                                       action.to.Modules.Cast<PartModule>().Any(x => x is KerbalEVA) ||
                                                       action.from.Modules.Cast<PartModule>().Any(x => x is KerbalEVA)))
      {
        // no SM crew Xfers in progress, so Non-override stock Xfers and EVAs require no action
        return;
      }

      if (SmVessel.TransferCrewObj.CrewXferActive)
      {
        // Remove the transfer message that stock displayed. 
        var failMessage =
          string.Format("<color=orange>{0} is unable to xfer to {1}.  An SM Crew Xfer is in progress</color>",
            action.host.name, action.to.partInfo.title);
        DisplayScreenMsg(failMessage);
        TransferCrew.RevertCrewTransfer(action.host, action.from, action.to);
      }
      else
      {
        //Check for DeepFreezer full. if full, abort handling Xfer.
        if (InstalledMods.IsDfInstalled && InstalledMods.IsDfApiReady && action.to.Modules.Contains("DeepFreezer"))
          if (new DFWrapper.DeepFreezer(action.to.Modules["DeepFreezer"]).FreezerSpace == 0) return;

        // If we are here, then we want to override the Stock Xfer...
        RemoveScreenMsg();

        // store data from event.
        SmVessel.TransferCrewObj.FromPart = action.from;
        SmVessel.TransferCrewObj.ToPart = action.to;
        SmVessel.TransferCrewObj.FromCrewMember = action.host;
        if (SmVessel.TransferCrewObj.FromPart != null && SmVessel.TransferCrewObj.ToPart != null)
          SmVessel.TransferCrewObj.IsStockXfer = true;
      }
    }

    //Vessel state handlers
    internal void OnVesselWasModified(Vessel modVessel)
    {
      try
      {
        SMHighlighter.ClearResourceHighlighting(SmVessel.SelectedResourcesParts);
        UpdateSMcontroller(modVessel);
      }
      catch (Exception ex)
      {
        Utilities.LogMessage("Error in:  SMAddon.OnVesselWasModified.  " + ex, "Error", true);
      }
    }

    internal void OnVesselChange(Vessel newVessel)
    {
      try
      {
        SMHighlighter.ClearResourceHighlighting(SmVessel.SelectedResourcesParts);
        UpdateSMcontroller(newVessel);
      }
      catch (Exception ex)
      {
        Utilities.LogMessage(
          string.Format(" in SMAddon.OnVesselChange.  Error:  {0} \r\n\r\n{1}", ex.Message, ex.StackTrace), "Error",
          true);
      }
    }

    private void OnVesselLoaded(Vessel data)
    {
      try
      {
        if (data.Equals(FlightGlobals.ActiveVessel) && data != SmVessel.Vessel)
        {
          SMHighlighter.ClearResourceHighlighting(SmVessel.SelectedResourcesParts);
          UpdateSMcontroller(data);
        }
      }
      catch (Exception ex)
      {
        Utilities.LogMessage("Error in:  SMAddon.OnVesselLoaded.  " + ex, "Error", true);
      }
    }

    // Stock vs Blizzy Toolbar switch handler
    private void CheckForToolbarTypeToggle()
    {
      if (SMSettings.EnableBlizzyToolbar && !SMSettings.PrevEnableBlizzyToolbar)
      {
        // Let't try to use Blizzy's toolbar
        if (!ActivateBlizzyToolBar())
        {
          // We failed to activate the toolbar, so revert to stock
          GameEvents.onGUIApplicationLauncherReady.Add(OnGuiAppLauncherReady);
          GameEvents.onGUIApplicationLauncherDestroyed.Add(OnGuiAppLauncherDestroyed);

          SMSettings.EnableBlizzyToolbar = SMSettings.PrevEnableBlizzyToolbar;
        }
        else
        {
          OnGuiAppLauncherDestroyed();
          GameEvents.onGUIApplicationLauncherReady.Remove(OnGuiAppLauncherReady);
          GameEvents.onGUIApplicationLauncherDestroyed.Remove(OnGuiAppLauncherDestroyed);
          SMSettings.PrevEnableBlizzyToolbar = SMSettings.EnableBlizzyToolbar;
          if (HighLogic.LoadedSceneIsFlight)
            _smButtonBlizzy.Visible = true;
          if (HighLogic.LoadedScene != GameScenes.SPACECENTER) return;
          _smRosterBlizzy.Visible = true;
          _smSettingsBlizzy.Visible = true;
        }
      }
      else if (!SMSettings.EnableBlizzyToolbar && SMSettings.PrevEnableBlizzyToolbar)
      {
        // Use stock Toolbar
        if (HighLogic.LoadedSceneIsFlight)
          _smButtonBlizzy.Visible = false;
        if (HighLogic.LoadedScene == GameScenes.SPACECENTER)
        {
          _smRosterBlizzy.Visible = false;
          _smSettingsBlizzy.Visible = false;
        }
        GameEvents.onGUIApplicationLauncherReady.Add(OnGuiAppLauncherReady);
        GameEvents.onGUIApplicationLauncherDestroyed.Add(OnGuiAppLauncherDestroyed);
        OnGuiAppLauncherReady();
        SMSettings.PrevEnableBlizzyToolbar = SMSettings.EnableBlizzyToolbar;
      }
    }

    // Stock Toolbar Startup and cleanup
    private void OnGuiAppLauncherReady()
    {
      try
      {
        // Setup SM WIndow button
        if (HighLogic.LoadedSceneIsFlight && _smButtonStock == null && !SMSettings.EnableBlizzyToolbar)
        {
          var iconfile = "IconOff_38";
          _smButtonStock = ApplicationLauncher.Instance.AddModApplication(
            OnSmButtonClicked,
            OnSmButtonClicked,
            DummyHandler,
            DummyHandler,
            DummyHandler,
            DummyHandler,
            ApplicationLauncher.AppScenes.FLIGHT | ApplicationLauncher.AppScenes.MAPVIEW,
            GameDatabase.Instance.GetTexture(TextureFolder + iconfile, false));

          if (WindowManifest.ShowWindow)
            _smButtonStock.SetTexture(
              GameDatabase.Instance.GetTexture(
                WindowManifest.ShowWindow ? TextureFolder + "IconOn_38" : TextureFolder + "IconOff_38", false));
        }

        // Setup Settings Button
        if (HighLogic.LoadedScene == GameScenes.SPACECENTER && _smSettingsStock == null &&
            !SMSettings.EnableBlizzyToolbar)
        {
          var iconfile = "IconS_Off_38";
          _smSettingsStock = ApplicationLauncher.Instance.AddModApplication(
            OnSmSettingsClicked,
            OnSmSettingsClicked,
            DummyHandler,
            DummyHandler,
            DummyHandler,
            DummyHandler,
            ApplicationLauncher.AppScenes.SPACECENTER,
            GameDatabase.Instance.GetTexture(TextureFolder + iconfile, false));

          if (WindowSettings.ShowWindow)
            _smSettingsStock.SetTexture(
              GameDatabase.Instance.GetTexture(
                WindowSettings.ShowWindow ? TextureFolder + "IconS_On_38" : TextureFolder + "IconS_Off_38", false));
        }

        // Setup Roster Button
        if (HighLogic.LoadedScene != GameScenes.SPACECENTER || _smRosterStock != null || SMSettings.EnableBlizzyToolbar)
          return;
        {
          var iconfile = "IconR_Off_38";
          _smRosterStock = ApplicationLauncher.Instance.AddModApplication(
            OnSmRosterClicked,
            OnSmRosterClicked,
            DummyHandler,
            DummyHandler,
            DummyHandler,
            DummyHandler,
            ApplicationLauncher.AppScenes.SPACECENTER,
            GameDatabase.Instance.GetTexture(TextureFolder + iconfile, false));

          if (WindowRoster.ShowWindow)
            _smRosterStock.SetTexture(
              GameDatabase.Instance.GetTexture(
                WindowRoster.ShowWindow ? TextureFolder + "IconR_On_38" : TextureFolder + "IconR_Off_38", false));
        }
      }
      catch (Exception ex)
      {
        Utilities.LogMessage("Error in:  SMAddon.OnGUIAppLauncherReady.  " + ex, "Error", true);
      }
    }

    private void OnGuiAppLauncherDestroyed()
    {
      //Debug.Log("[ShipManifest]:  ShipManifestAddon.OnGUIAppLauncherDestroyed");
      try
      {
        if (_smButtonStock != null)
        {
          ApplicationLauncher.Instance.RemoveModApplication(_smButtonStock);
          _smButtonStock = null;
        }
        if (_smRosterStock != null)
        {
          ApplicationLauncher.Instance.RemoveModApplication(_smRosterStock);
          _smRosterStock = null;
        }
        if (_smSettingsStock == null) return;
        ApplicationLauncher.Instance.RemoveModApplication(_smSettingsStock);
        _smSettingsStock = null;
      }
      catch (Exception ex)
      {
        Utilities.LogMessage("Error in:  SMAddon.OnGUIAppLauncherDestroyed.  " + ex, "Error", true);
      }
    }

    //Toolbar button click handlers
    internal static void OnSmButtonClicked()
    {
      //Debug.Log("[ShipManifest]:  ShipManifestAddon.OnSMButtonToggle");
      try
      {
        if (WindowManifest.ShowWindow)
        {
          // SM is showing.  Turn off.
          if (SmVessel.TransferCrewObj.CrewXferActive || TransferPump.PumpProcessOn)
            return;

          SMHighlighter.ClearResourceHighlighting(SmVessel.SelectedResourcesParts);
          SmVessel.SelectedResources.Clear();
          SmVessel.SelectedPartsSource.Clear();
          SmVessel.SelectedPartsTarget.Clear();
          WindowManifest.ShowWindow = !WindowManifest.ShowWindow;
        }
        else
        {
          // SM is not showing. turn on if we can.
          if (SMConditions.CanShowShipManifest(true))
            WindowManifest.ShowWindow = !WindowManifest.ShowWindow;
          else
            return;
        }

        if (SMSettings.EnableBlizzyToolbar)
          _smButtonBlizzy.TexturePath = WindowManifest.ShowWindow
            ? TextureFolder + "IconOn_24"
            : TextureFolder + "IconOff_24";
        else
          _smButtonStock.SetTexture(
            GameDatabase.Instance.GetTexture(
              WindowManifest.ShowWindow ? TextureFolder + "IconOn_38" : TextureFolder + "IconOff_38", false));
      }
      catch (Exception ex)
      {
        Utilities.LogMessage("Error in:  SMAddon.OnSMButtonToggle.  " + ex, "Error", true);
      }
    }

    internal static void OnSmRosterClicked()
    {
      //Debug.Log("[ShipManifest]:  SMAddon.OnSMRosterToggle");
      try
      {
        if (HighLogic.LoadedScene == GameScenes.SPACECENTER)
        {
          WindowRoster.ShowWindow = !WindowRoster.ShowWindow;
          if (SMSettings.EnableBlizzyToolbar)
            _smRosterBlizzy.TexturePath = WindowRoster.ShowWindow
              ? TextureFolder + "IconR_On_24"
              : TextureFolder + "IconR_Off_24";
          else
            _smRosterStock.SetTexture(
              GameDatabase.Instance.GetTexture(
                WindowRoster.ShowWindow ? TextureFolder + "IconR_On_38" : TextureFolder + "IconR_Off_38", false));
          if (WindowRoster.ShowWindow) WindowRoster.GetRosterList();
        }
      }
      catch (Exception ex)
      {
        Utilities.LogMessage("Error in:  SMAddon.OnSMRosterToggle.  " + ex, "Error", true);
      }
    }

    internal static void OnSmSettingsClicked()
    {
      //Debug.Log("[ShipManifest]:  SMAddon.OnSMRosterToggle. Val:  " + WindowSettings.ShowWindow);
      try
      {
        if (HighLogic.LoadedScene == GameScenes.SPACECENTER)
        {
          WindowSettings.ShowWindow = !WindowSettings.ShowWindow;
          SMSettings.MemStoreTempSettings();
          if (SMSettings.EnableBlizzyToolbar)
            _smSettingsBlizzy.TexturePath = WindowSettings.ShowWindow
              ? TextureFolder + "IconS_On_24"
              : TextureFolder + "IconS_Off_24";
          else
            _smSettingsStock.SetTexture(
              GameDatabase.Instance.GetTexture(
                WindowSettings.ShowWindow ? TextureFolder + "IconS_On_38" : TextureFolder + "IconS_Off_38", false));
        }
      }
      catch (Exception ex)
      {
        Utilities.LogMessage("Error in:  SMAddon.OnSMSettingsToggle.  " + ex, "Error", true);
      }
    }

    #endregion

    #region GUI Methods

    internal void Display()
    {
      var step = "";
      try
      {
        step = "0 - Start";
        if (WindowDebugger.ShowWindow)
          WindowDebugger.Position = GUILayout.Window(398643, WindowDebugger.Position, WindowDebugger.Display,
            WindowDebugger.Title, GUILayout.MinHeight(20));

        if ((HighLogic.LoadedScene == GameScenes.FLIGHT || HighLogic.LoadedScene == GameScenes.SPACECENTER) && ShowUi)
        {
          if (WindowSettings.ShowWindow)
          {
            step = "4 - Show Settings";
            WindowSettings.Position = GUILayout.Window(398546, WindowSettings.Position, WindowSettings.Display,
              WindowSettings.Title, GUILayout.MinHeight(20));
          }

          if (WindowRoster.ShowWindow)
          {
            step = "6 - Show Roster";
            if (WindowRoster.ResetRosterSize)
              WindowRoster.Position.height = SMSettings.UseUnityStyle ? 330 : 350;
            WindowRoster.Position = GUILayout.Window(398547, WindowRoster.Position, WindowRoster.Display,
              WindowRoster.Title, GUILayout.MinHeight(20));
          }
        }
        if (HighLogic.LoadedScene == GameScenes.FLIGHT &&
            (FlightGlobals.fetch == null ||
             (FlightGlobals.ActiveVessel != null && FlightGlobals.ActiveVessel != SmVessel.Vessel)))
        {
          step = "0a - Vessel Change";
          SmVessel.SelectedPartsSource.Clear();
          SmVessel.SelectedPartsTarget.Clear();
          SmVessel.SelectedResources.Clear();
          return;
        }

        step = "1 - Show Interface(s)";
        // Is the scene one we want to be visible in?
        if (SMConditions.CanShowShipManifest())
        {
          // What windows do we want to show?
          step = "2 - Can Show Manifest - true";
          WindowManifest.Position = GUILayout.Window(398544, WindowManifest.Position, WindowManifest.Display,
            WindowManifest.Title, GUILayout.MinHeight(20));

          if (WindowTransfer.ShowWindow && SmVessel.SelectedResources.Count > 0)
          {
            step = "3 - Show Transfer";
            // Lets build the running totals for each resource for display in title...
            WindowTransfer.Position = GUILayout.Window(398545, WindowTransfer.Position, WindowTransfer.Display,
              WindowTransfer.Title, GUILayout.MinHeight(20));
          }

          if (!WindowManifest.ShowWindow || !WindowControl.ShowWindow) return;
          step = "7 - Show Control";
          WindowControl.Position = GUILayout.Window(398548, WindowControl.Position, WindowControl.Display,
            WindowControl.Title, GUILayout.MinWidth(350), GUILayout.MinHeight(20));
        }
        else
        {
          step = "2 - Can Show Manifest = false";
          if (!SMSettings.EnableCls || SmVessel == null) return;
          if (SmVessel.SelectedResources.Contains(SMConditions.ResourceType.Crew.ToString()))
            SMHighlighter.HighlightClsVessel(false, true);
        }
      }
      catch (Exception ex)
      {
        if (!FrameErrTripped)
        {
          Utilities.LogMessage(
            string.Format(" in Display at or near step:  " + step + ".  Error:  {0} \r\n\r\n{1}", ex.Message,
              ex.StackTrace), "Error", true);
          FrameErrTripped = true;
        }
      }
    }

    internal static void RepositionWindows()
    {
      RepositionWindow(ref WindowManifest.Position);
      RepositionWindow(ref WindowTransfer.Position);
      RepositionWindow(ref WindowDebugger.Position);
      RepositionWindow(ref WindowSettings.Position);
      RepositionWindow(ref WindowControl.Position);
      RepositionWindow(ref WindowRoster.Position);
    }

    internal static void RepositionWindow(ref Rect windowPosition)
    {
      if (windowPosition.x < 0)
        windowPosition.x = 0;
      if (windowPosition.y < 0)
        windowPosition.y = 0;
      if (windowPosition.xMax > Screen.currentResolution.width)
        windowPosition.x = Screen.currentResolution.width - windowPosition.width;
      if (windowPosition.yMax > Screen.currentResolution.height)
        windowPosition.y = Screen.currentResolution.height - windowPosition.height;
    }

    #endregion

    #region Action Methods

    internal void UpdateSMcontroller(Vessel newVessel)
    {
      try
      {
        SMHighlighter.ClearResourceHighlighting(SmVessel.SelectedResourcesParts);
        if (SmVessel.Vessel != newVessel)
        {
          if (SmVessel.TransferCrewObj.CrewXferActive && !SmVessel.TransferCrewObj.IvaDelayActive)
            SmVessel.TransferCrewObj.CrewTransferAbort();
          if (TransferPump.PumpProcessOn) TransferPump.PumpProcessOn = false;
        }

        if (SmVessel.Vessel != null && SMConditions.CanShowShipManifest())
        {
          if (newVessel.isEVA && !SmVessel.Vessel.isEVA)
          {
            if (WindowManifest.ShowWindow) OnSmButtonClicked();

            // kill selected resource and its associated highlighting.
            SmVessel.SelectedResources.Clear();
          }
        }

        // Now let's update the current vessel view...
        SmVessel = SMVessel.GetInstance(newVessel);
        SmVessel.RefreshLists();
      }
      catch (Exception ex)
      {
        Utilities.LogMessage("Error in:  SMAddon.UpdateSMcontroller.  " + ex, "Error", true);
      }
    }

    internal static void UpdateClsSpaces()
    {
      if (GetClsVessel())
      {
        try
        {
          SmVessel.ClsPartSource = null;
          SmVessel.ClsSpaceSource = null;
          SmVessel.ClsPartTarget = null;
          SmVessel.ClsSpaceTarget = null;
          foreach (var sSpace in ClsAddon.Vessel.Spaces)
          {
            foreach (var sPart in sSpace.Parts)
            {
              if (SmVessel.SelectedPartsSource.Contains(sPart.Part) && SmVessel.ClsPartSource == null)
              {
                SmVessel.ClsPartSource = sPart;
                SmVessel.ClsSpaceSource = sSpace;
              }
              if (SmVessel.SelectedPartsTarget.Contains(sPart.Part) && SmVessel.ClsPartTarget == null)
              {
                SmVessel.ClsPartTarget = sPart;
                SmVessel.ClsSpaceTarget = sSpace;
              }
              if (SmVessel.ClsPartSource != null && SmVessel.ClsPartTarget != null)
                break;
            }
            if (SmVessel.ClsSpaceSource != null && SmVessel.ClsSpaceTarget != null)
              break;
          }
        }
        catch (Exception ex)
        {
          Utilities.LogMessage(
            string.Format(" in UpdateCLSSpaces.  Error:  {0} \r\n\r\n{1}", ex.Message, ex.StackTrace), "Error", true);
        }
      }
    }

    internal static bool GetClsAddon()
    {
      ClsAddon = ClsClient.GetCls();
      if (ClsAddon == null)
      {
        return false;
      }
      return true;
    }

    internal static bool GetClsVessel()
    {
      try
      {

        if (ClsAddon.Vessel != null)
        {
          return true;
        }
        return false;
      }
      catch (Exception ex)
      {
        Utilities.LogMessage(
          string.Format(" in SMAddon.GetCLSVessel.  Error:  {0} \r\n\r\n{1}", ex.Message, ex.StackTrace), "Error", true);
        return false;
      }
    }

    internal static bool ActivateBlizzyToolBar()
    {
      if (SMSettings.EnableBlizzyToolbar)
      {
        try
        {
          if (ToolbarManager.ToolbarAvailable)
          {
            if (HighLogic.LoadedScene == GameScenes.FLIGHT)
            {
              _smButtonBlizzy = ToolbarManager.Instance.add("ShipManifest", "Manifest");
              _smButtonBlizzy.TexturePath = WindowManifest.ShowWindow
                ? TextureFolder + "IconOn_24"
                : TextureFolder + "IconOff_24";
              _smButtonBlizzy.ToolTip = "Ship Manifest";
              _smButtonBlizzy.Visibility = new GameScenesVisibility(GameScenes.FLIGHT);
              _smButtonBlizzy.Visible = true;
              _smButtonBlizzy.OnClick += e => { OnSmButtonClicked(); };
            }

            if (HighLogic.LoadedScene == GameScenes.SPACECENTER)
            {
              _smSettingsBlizzy = ToolbarManager.Instance.add("ShipManifest", "Settings");
              _smSettingsBlizzy.TexturePath = WindowSettings.ShowWindow
                ? TextureFolder + "IconS_On_24"
                : TextureFolder + "IconS_Off_24";
              _smSettingsBlizzy.ToolTip = "Ship Manifest Settings Window";
              _smSettingsBlizzy.Visibility = new GameScenesVisibility(GameScenes.SPACECENTER);
              _smSettingsBlizzy.Visible = true;
              _smSettingsBlizzy.OnClick += e => { OnSmSettingsClicked(); };

              _smRosterBlizzy = ToolbarManager.Instance.add("ShipManifest", "Roster");
              _smRosterBlizzy.TexturePath = WindowRoster.ShowWindow
                ? TextureFolder + "IconR_On_24"
                : TextureFolder + "IconR_Off_24";
              _smRosterBlizzy.ToolTip = "Ship Manifest Roster Window";
              _smRosterBlizzy.Visibility = new GameScenesVisibility(GameScenes.SPACECENTER);
              _smRosterBlizzy.Visible = true;
              _smRosterBlizzy.OnClick += e => { OnSmRosterClicked(); };
            }
            return true;
          }
          return false;
        }
        catch (Exception)
        {
          // Blizzy Toolbar instantiation error.
          return false;
        }
      }
      // No Blizzy Toolbar
      return false;
    }

    internal static void FireEventTriggers()
    {
      // Per suggestion by shaw (http://forum.kerbalspaceprogram.com/threads/62270?p=1033866&viewfull=1#post1033866)
      // and instructions for using CLS API by codepoet.
      GameEvents.onVesselChange.Fire(SmVessel.Vessel);
    }

    internal static void DisplayScreenMsg(string strMessage)
    {
      var smessage = new ScreenMessage(string.Empty, 15f, ScreenMessageStyle.LOWER_CENTER);
      var smessages = FindObjectOfType<ScreenMessages>();
      if (smessages != null)
      {
        var smessagesToRemove =
          smessages.activeMessages.Where(
            x =>
              Math.Abs(x.startTime - smessage.startTime) < SMSettings.Tolerance &&
              x.style == ScreenMessageStyle.LOWER_CENTER).ToList();
        foreach (var m in smessagesToRemove)
          ScreenMessages.RemoveMessage(m);
        var failmessage = new ScreenMessage(string.Empty, 15f, ScreenMessageStyle.UPPER_CENTER);
        ScreenMessages.PostScreenMessage(strMessage, failmessage, true);
      }
    }

    internal static void RemoveScreenMsg()
    {
      var smessage = new ScreenMessage(string.Empty, 15f, ScreenMessageStyle.LOWER_CENTER);
      var smessages = FindObjectOfType<ScreenMessages>();
      if (smessages != null)
      {
        var smessagesToRemove =
          smessages.activeMessages.Where(
            x =>
              Math.Abs(x.startTime - smessage.startTime) < SMSettings.Tolerance &&
              x.style == ScreenMessageStyle.LOWER_CENTER).ToList();
        foreach (var m in smessagesToRemove)
          ScreenMessages.RemoveMessage(m);
      }
    }

    // This method is used for autosave...
    internal void RunSave()
    {
      try
      {
        SMSettings.SaveSettings();
      }
      catch (Exception ex)
      {
        Utilities.LogMessage(string.Format(" in SMAddon.RunSave.  Error:  {0} \r\n\r\n{1}", ex.Message, ex.StackTrace),
          "Error", true);
      }
    }

    #endregion
  }

  internal class ShipManifestModule : PartModule
  {
    [KSPEvent(guiActive = true, guiName = "Destroy Part", active = true)]
    internal void DestoryPart()
    {
      if (part != null)
        part.temperature = 5000;
    }

    public override void OnUpdate()
    {
      base.OnUpdate();

      if (part != null && part.name == "ShipManifest")
        Events["DestoryPart"].active = true;
      else
        Events["DestoryPart"].active = false;
    }
  }
}