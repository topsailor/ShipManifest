using System;
using System.Collections.Generic;
using System.Linq;
using ConnectedLivingSpace;
using ShipManifest.Modules;
using ShipManifest.Process;
using ShipManifest.Windows;

namespace ShipManifest
{
  // ReSharper disable once InconsistentNaming
  internal class SMVessel
  {
    #region Static Singleton stuff

    // ReSharper disable once FieldCanBeMadeReadOnly.Local
    private static Dictionary<WeakReference<Vessel>, SMVessel> _controllers = new Dictionary<WeakReference<Vessel>, SMVessel>();

    internal static SMVessel GetInstance(Vessel vessel)
    {
      foreach (var kvp in _controllers.ToArray())
      {
        var wr = kvp.Key;
        var v = wr.Target;
        if (v == null)
        {
          _controllers.Remove(wr);
        }
        else if (v == vessel)
        {
          return _controllers[wr];
        }
      }

      var commander = new SMVessel();
      _controllers[new WeakReference<Vessel>(vessel)] = commander;
      return commander;
    }

    #endregion

    #region Constructor(s)

    #endregion

    #region Instance Properties

    // variables used for tracking xfer sliders for resources.
    internal double AmtXferred = 0;

    internal Vessel Vessel
    {
      get { return _controllers.Single(p => p.Value == this).Key.Target; }
    }

    internal bool IsRecoverable
    {
      get { return Vessel.IsRecoverable; }
    }

    #endregion

    #region Instance DataSource properties

    // dataSources for Resource manifest and ResourceTransfer windows


    // Provides a list of resources and the parts that contain that resource.
    internal List<string> ResourceList = new List<string>();
    private Dictionary<string, List<Part>> _partsByResource;
    internal Dictionary<string, List<Part>> PartsByResource
    {
      get
      {
        try
        {
          if (_partsByResource == null)
            _partsByResource = new Dictionary<string, List<Part>>();
          else
            _partsByResource.Clear();

          // Let's update...
          if (FlightGlobals.ActiveVessel != null)
          {
            //Utilities.LogMessage(string.Format(" getting partsbyresource.  "), "Info", SettingsManager.VerboseLogging);

            _partsByResource = new Dictionary<string, List<Part>>();
            foreach (var part in Vessel.Parts)
            {
              // First let's Get any Crew, if desired...
              if (SMSettings.EnableCrew && part.CrewCapacity > 0 && part.partInfo.name != "kerbalEVA")
              {
                var vResourceFound = false;
                // is resource in the list yet?.
                if (_partsByResource.Keys.Contains("Crew"))
                {
                  // found resource.  lets add part to its list.
                  vResourceFound = true;
                  var eParts = _partsByResource["Crew"];
                  eParts.Add(part);
                }
                if (!vResourceFound)
                {
                  // found a new resource.  lets add it to the list of resources.
                  var nParts = new List<Part> {part};
                  _partsByResource.Add("Crew", nParts);
                }
              }
              // Let's Get any Science...
              if (SMSettings.EnableScience)
              {
                var sciModules = part.FindModulesImplementing<IScienceDataContainer>().ToArray();
                if (sciModules.Length > 0)
                {
                  // is resource in the list yet?.
                  // We only need the first match on the part so stop.
                  if (_partsByResource.Keys.Contains("Science"))
                  {
                    _partsByResource["Science"].Add(part);
                  }
                  else
                  {
                    // found a new resource.  lets add it to the list of resources.
                    var nParts = new List<Part> {part};
                    _partsByResource.Add("Science", nParts);                                     
                  }
                }
              }

              // Now, let's get flight Resources.
              if (SMSettings.EnableResources)
              {
                foreach (PartResource resource in part.Resources)
                {
                  // Realism Mode.  we want to exclude Resources with TransferMode = NONE...
                  if (!SMSettings.RealismMode || (SMSettings.RealismMode && resource.info.resourceTransferMode != ResourceTransferMode.NONE))
                  {
                    var vResourceFound = false;
                    // is resource in the list yet?.
                    if (_partsByResource.Keys.Contains(resource.info.name))
                    {
                      vResourceFound = true;
                      var eParts = _partsByResource[resource.info.name];
                      eParts.Add(part);
                    }
                    if (!vResourceFound)
                    {
                      // found a new resource.  lets add it to the list of resources.
                      var nParts = new List<Part> {part};
                      _partsByResource.Add(resource.info.name, nParts);
                    }
                  }
                }
              }
            }
          }
        }
        catch (Exception ex)
        {
          Utilities.LogMessage(string.Format(" getting partsbyresource.  {0} \r\n\r\n{1}", ex.Message, ex.StackTrace), "Error", true);
          _partsByResource = null;
        }

        if (_partsByResource != null)
          ResourceList = new List<string>(_partsByResource.Keys);
        else
          ResourceList.Clear();

        return _partsByResource;
      }
    }

    // dataSource for Resource manifest and ResourceTransfer windows
    // Holds the Resource.info.name selected in the Resource Manifest Window.
    internal List<string> SelectedResources = new List<string>();
    internal List<TransferResource> ResourcesToXfer = new List<TransferResource>();

    // Multi-Part Xfer Storage
    private List<ModDockedVessel> _dockedVessels;
    internal List<ModDockedVessel> DockedVessels
    {
      get
      {
        if (_dockedVessels == null)
        {
          _dockedVessels = new List<ModDockedVessel>();
          var dockingParts = (from p in Vessel.parts where p.Modules.Contains("ModuleDockingNode") select p).ToList();
          foreach (var dPart in dockingParts)
          {
            var dNodes = (from PartModule m in dPart.Modules where m.moduleName == "ModuleDockingNode" select m).ToList();
            foreach (var pModule in dNodes)
            {
              var dockedInfo = ((ModuleDockingNode)pModule).vesselInfo;
              if (dockedInfo != null)
              {
                var modDockedVessel = new ModDockedVessel(dockedInfo);
                var launchIds = (from m in _dockedVessels where m.LaunchId > 0 select m.LaunchId).ToList();
                if (!launchIds.Contains(modDockedVessel.LaunchId))
                  _dockedVessels.Add(modDockedVessel);
              }
            }
          }
        }
        return _dockedVessels;
      }
      set
      {
        _dockedVessels = value;
      }
    }
    internal List<ModDockedVessel> SelectedVesselsSource = new List<ModDockedVessel>();
    internal List<ModDockedVessel> SelectedVesselsTarget = new List<ModDockedVessel>();

    internal List<Part> SelectedResourcesParts = new List<Part>();
    internal List<Part> SelectedPartsSource = new List<Part>();
    internal List<Part> SelectedPartsTarget = new List<Part>();

    internal PartModule SelectedModuleSource;
    internal PartModule SelectedModuleTarget;

    internal ICLSPart ClsPartSource;
    internal ICLSPart ClsPartTarget;
    internal ICLSSpace ClsSpaceSource;
    internal ICLSSpace ClsSpaceTarget;

    internal TransferCrew TransferCrewObj = new TransferCrew();

    // Control Window parts
    private List<ModHatch> _hatches = new List<ModHatch>();
    internal List<ModHatch> Hatches
    {
      get { return _hatches ?? (_hatches = new List<ModHatch>()); }
      set
      {
        _hatches.Clear();
        _hatches = value;
      }
    }

    private List<ModSolarPanel> _solarPanels = new List<ModSolarPanel>();
    internal List<ModSolarPanel> SolarPanels
    {
      get { return _solarPanels ?? (_solarPanels = new List<ModSolarPanel>()); }
      set
      {
        _solarPanels.Clear();
        _solarPanels = value;
      }
    }

    private List<ModLight> _lights = new List<ModLight>();
    internal List<ModLight> Lights
    {
      get { return _lights ?? (_lights = new List<ModLight>()); }
      set
      {
        _lights.Clear();
        _lights = value;
      }
    }

    private List<ModAntenna> _antennas = new List<ModAntenna>();
    internal List<ModAntenna> Antennas
    {
      get { return _antennas ?? (_antennas = new List<ModAntenna>()); }
      set
      {
        _antennas.Clear();
        _antennas = value;
      }
    }

    #endregion

    #region Instance DataSource Methods

    internal void RefreshLists()
    {
      GetSelectedResourcesParts(true);
      _dockedVessels = null;

      // now lets reconcile the selected parts based on the new list of resources...
      WindowManifest.ReconcileSelectedXferParts(SMAddon.SmVessel.SelectedResources);

      // Now lets update the Resource Xfer Objects...
      SMAddon.SmVessel.ResourcesToXfer.Clear();
      if (!SMAddon.SmVessel.SelectedResources.Contains("Crew") && !SMAddon.SmVessel.SelectedResources.Contains("Science"))
      {
        foreach (var resource in SMAddon.SmVessel.SelectedResources)
        {
          // Lets create a Xfer Object for managing xfer options and data.
          var modResource = new TransferResource(resource)
          {
            SrcXferAmount =
              TransferResource.CalcMaxResourceXferAmt(SMAddon.SmVessel.SelectedPartsSource,
                SMAddon.SmVessel.SelectedPartsTarget, resource),
            TgtXferAmount =
              TransferResource.CalcMaxResourceXferAmt(SMAddon.SmVessel.SelectedPartsTarget,
                SMAddon.SmVessel.SelectedPartsSource, resource)
          };
          SMAddon.SmVessel.ResourcesToXfer.Add(modResource);
        }
      }

      if (SMSettings.EnableCls && SMAddon.CanShowShipManifest())
      {
        if (SMAddon.GetClsAddon())
        {
          SMAddon.UpdateClsSpaces();
          if (SMAddon.GetClsVessel())
            GetHatches();
        }
      }

      SMAddon.FrozenKerbals = WindowRoster.GetFrozenKerbals();

      GetAntennas();
      GetLights();
      GetSolarPanels();
      WindowRoster.GetRosterList();
      DockedVessels = null;
    }

    internal void GetSelectedResourcesParts(bool refresh = false)
    {
      switch (SelectedResources.Count)
      {
        case 1:
          SelectedResourcesParts = refresh ? PartsByResource[SelectedResources[0]] : _partsByResource[SelectedResources[0]];
          break;
        case 2:
          SelectedResourcesParts.Clear();
          foreach (var part in Vessel.Parts.Where(part => part.Resources.Contains(SelectedResources[0]) && part.Resources.Contains(SelectedResources[1])))
          {
            SelectedResourcesParts.Add(part);
          }
          break;
      }
    }

    internal void GetHatches()
    {
      _hatches.Clear();
      try
      {
        foreach (var iPart in SMAddon.ClsAddon.Vessel.Parts)
        {
          foreach (PartModule pModule in iPart.Part.Modules)
          {
            if (pModule.moduleName == "ModuleDockingHatch")
            {
              var pHatch = new ModHatch
              {
                HatchModule = pModule,
                ClsPart = iPart
              };
              _hatches.Add(pHatch);
            }
          }
        }
      }
      catch (Exception ex)
      {
        Utilities.LogMessage(string.Format("Error in GetHatches().\r\nError:  {0}", ex), "Error", true);
      }
    }

    internal void GetSolarPanels()
    {
      _solarPanels.Clear();
      try
      {
        foreach (var pPart in Vessel.Parts)
        {
          foreach (PartModule pModule in pPart.Modules)
          {
            if (pModule.moduleName == "ModuleDeployableSolarPanel")
            {
              var iModule = (ModuleDeployableSolarPanel)pModule;
              if (iModule.Events["Extend"].active || iModule.Events["Retract"].active)
              {
                var pPanel = new ModSolarPanel
                {
                  PanelModule = pModule,
                  SPart = pPart
                };
                _solarPanels.Add(pPanel);
              }
            }
          }
        }
      }
      catch (Exception ex)
      {
        Utilities.LogMessage(string.Format("Error in GetSolarPanels().\r\nError:  {0}", ex), "Error", true);
      }
    }

    internal void GetAntennas()
    {
      _antennas.Clear();
      try
      {
        // Added support for RemoteTech antennas
        foreach (var pPart in Vessel.Parts)
        {
          if (pPart.Modules.Contains("ModuleDataTransmitter") || pPart.Modules.Contains("ModuleRTAntenna"))
          {
            var pAntenna = new ModAntenna {SPart = pPart};
            foreach (PartModule pModule in pPart.Modules)
            {
              if (pModule.moduleName == "ModuleDataTransmitter" || pModule.moduleName == "ModuleRTAntenna")
              {
                pAntenna.XmitterModule = pModule;
              }
              if (pModule.moduleName == "ModuleAnimateGeneric" && (pModule.Events["Toggle"].guiName == "Extend" || pModule.Events["Toggle"].guiName == "Retract"))
              {
                pAntenna.AnimateModule = pModule;
              }
            }
            _antennas.Add(pAntenna);
          }
        }
      }
      catch (Exception ex)
      {
        Utilities.LogMessage(string.Format("Error in GetAntennas().\r\nError:  {0}", ex), "Error", true);
      }
    }

    internal void GetLights()
    {
      _lights.Clear();
      try
      {
        foreach (var pPart in Vessel.Parts)
        {
          var part = pPart;
          foreach (var pLight in from PartModule pModule in pPart.Modules where pModule.moduleName == "ModuleLight" select new ModLight
          {
            LightModule = pModule,
            SPart = part
          })
          {
            _lights.Add(pLight);
            break;
          }
        }
      }
      catch (Exception ex)
      {
        Utilities.LogMessage(string.Format("Error in GetLights().\r\nError:  {0}", ex), "Error", true);
      }
    }

    internal List<Part> GetSelectedVesselsParts(List<ModDockedVessel> modDockedVessels, List<string> selectedResources)
    {
      var resourcePartList = new List<Part>();
      try
      {
        if (modDockedVessels != null && modDockedVessels.Count > 0)
        {
          foreach (var modDockedvessel in modDockedVessels)
          {
            resourcePartList.AddRange(selectedResources.Count > 1
              ? (from p in modDockedvessel.VesselParts
                where p.Resources.Contains(selectedResources[0]) && p.Resources.Contains(selectedResources[1])
                select p).ToList()
              : (from p in modDockedvessel.VesselParts where p.Resources.Contains(selectedResources[0]) select p).ToList
                ());
          }
        }
      }
      catch (Exception ex)
      {
        Utilities.LogMessage(string.Format("Error in GetSelectedVesselParts().\r\nError:  {0}", ex), "Error", true);
        resourcePartList = new List<Part>();
      }
      return resourcePartList;
    }

    internal List<Part> GetDockedVesselParts(DockedVesselInfo vesselInfo)
    {
      var vesselpartList = new List<Part>();
      try
      {
        if (vesselInfo != null)
        {
          var vesselRoot = (from p in Vessel.parts where p.flightID == vesselInfo.rootPartUId select p).SingleOrDefault();
          if (vesselRoot != null)
          {
            vesselpartList = (from p in Vessel.parts where p.launchID == vesselRoot.launchID select p).ToList();
          }
        }
      }
      catch (Exception ex)
      {
        Utilities.LogMessage(string.Format("Error in GetSelectedVesselParts().\r\nError:  {0}", ex), "Error", true);
        vesselpartList = new List<Part>();
      }
      return vesselpartList;
    }

    #endregion

    #region Action Methods

    internal Part FindPartByKerbal(ProtoCrewMember pKerbal)
    {
      var kPart = FlightGlobals.ActiveVessel.Parts.Find(x => x.protoModuleCrew.Find(y => y == pKerbal) != null);
      return kPart;
    }

    internal void RespawnCrew()
    {
      Vessel.SpawnCrew();
      SMAddon.FireEventTriggers();
    }

    internal void FillCrew()
    {
      foreach (var part in _partsByResource["Crew"])
      {
        SMPart.FillCrew(part);
      }
      SMAddon.FireEventTriggers();
    }

    internal void EmptyCrew()
    {
      foreach (var part in _partsByResource["Crew"])
      {
        for (var i = part.protoModuleCrew.Count - 1; i >= 0; i--)
        {
          TransferCrew.RemoveCrewMember(part.protoModuleCrew[i], part);
        }
        SMAddon.FireEventTriggers();
      }
    }

    internal void FillResources()
    {
      var resources = _partsByResource.Keys.ToList();
      foreach (var resource in resources.Where(resourceName => resourceName != "Crew" && resourceName != "Science").SelectMany(resourceName => (from part in _partsByResource[resourceName] from PartResource resource in part.Resources where resource.info.name == resourceName select resource)))
      {
        resource.amount = resource.maxAmount;
      }
    }

    internal void DumpResources()
    {
      var resources = _partsByResource.Keys.ToList();
      foreach (var resource in from resourceName in resources where resourceName != "Crew" && resourceName != "Science" from part in _partsByResource[resourceName] from PartResource resource in part.Resources where resource.info.name == resourceName select resource)
      {
        resource.amount = 0;
      }
    }

    internal void DumpResource(string resourceName)
    {
      foreach (var part in _partsByResource[resourceName])
      {
        foreach (PartResource resource in part.Resources)
        {
          if (resource.info.name == resourceName)
          {
            resource.amount = 0;
          }
        }
      }
    }

    internal void FillResource(string resourceName)
    {
      foreach (var part in _partsByResource[resourceName])
      {
        foreach (PartResource resource in part.Resources)
        {
          if (resource.info.name == resourceName)
          {
            resource.amount = resource.maxAmount;
          }
        }
      }
    }

    #endregion
  }
}