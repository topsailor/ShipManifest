using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using ConnectedLivingSpace;

namespace ShipManifest
{
    public partial class ManifestController
    {
        #region Properties
        // Transfer Controller.  This module contains Resource Manifest specific code. 
        // I made it a partial class to allow file level segregation of resource and crew functionality
        // for improved readability and management by coders.

        // variables used for moving resources.  set to a negative to allow slider to function.
        public float sXferAmount = -1f;
        public float tXferAmount = -1f;
        public float AmtXferred = 0f;

        // Flags to show windows
        public bool ShowTransferWindow { get; set; }
        private bool _showShipManifest = false;
        public bool ShowShipManifest 
        {
            get
            {
                return _showShipManifest;
            }
            set
            {
                if (value == false)
                {
                    if (ShipManifestBehaviour.clsVessel != null)
                    {
                        foreach (CLSSpace space in ShipManifestBehaviour.clsVessel.Spaces)
                        {
                            space.Highlight(false);
                        }
                    }
               }
                _showShipManifest = value;
            }
        }
        #endregion

        #region GUI Layout TransferWindow)

        // Resource Transfer Window
        // This window allows you some control over the selected resource on a selected source and target part
        // This window assumes that a resource has been selected on the Ship manifest window.
        private Vector2 SourceScrollViewerTransfer = Vector2.zero;
        private Vector2 SourceScrollViewerTransfer2 = Vector2.zero;
        private Vector2 TargetScrollViewerTransfer = Vector2.zero;
        private Vector2 TargetScrollViewerTransfer2 = Vector2.zero;
        private void TransferWindow(int windowId)
        {
            try
            {
                // This window assumes that a resource has been selected on the Ship manifest window.
                GUILayout.BeginHorizontal();
                //Left Column Begins
                GUILayout.BeginVertical();

                // Build source Transfer Viewer
                SourceTransferViewer();

                // Text above Source Details. (Between viewers)
                if (ShipManifestBehaviour.SelectedResource == "Crew" && SettingsManager.ShowIVAUpdate)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(ShipManifestBehaviour.SelectedPartSource != null ? string.Format("{0}", ShipManifestBehaviour.SelectedPartSource.partInfo.title) : "No Part Selected", GUILayout.Width(190), GUILayout.Height(20));
                    if (GUILayout.Button("Update Portraits", ManifestStyle.ButtonStyle, GUILayout.Width(110), GUILayout.Height(20)))
                    {
                        RespawnCrew();
                    }
                    GUILayout.EndHorizontal();
                }
                else
                {
                    GUILayout.Label(ShipManifestBehaviour.SelectedPartSource != null ? string.Format("{0}", ShipManifestBehaviour.SelectedPartSource.partInfo.title) : "No Part Selected", GUILayout.Width(300), GUILayout.Height(20));
                }

                // Build Details ScrollViewer
                SourceDetailsViewer();

                // Okay, we are done with the left column of the dialog...
                GUILayout.EndVertical();

                // Right Column Begins...
                GUILayout.BeginVertical();

                // Build Target Transfer Viewer
                TargetTransferViewer();

                // Text between viewers
                GUILayout.Label(ShipManifestBehaviour.SelectedPartTarget != null ? string.Format("{0}", ShipManifestBehaviour.SelectedPartTarget.partInfo.title) : "No Part Selected", GUILayout.Width(300), GUILayout.Height(20));

                // Build Target details Viewer
                TargetDetailsViewer();
                
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
                GUI.DragWindow(new Rect(0, 0, Screen.width, 30));
            }
            catch (Exception ex)
            {
                ManifestUtilities.LogMessage(" in Ship Manifest Window.  Error:  " + ex.ToString(), "Error", true);
            }
        }

        // Transfer Window components
        private void SourceTransferViewer()
        {
            try
            {
                // This is a scroll panel (we are using it to make button lists...)
                SourceScrollViewerTransfer = GUILayout.BeginScrollView(SourceScrollViewerTransfer, GUILayout.Height(120), GUILayout.Width(300));
                GUILayout.BeginVertical();

                foreach (Part part in ShipManifestBehaviour.SelectedResourceParts)
                {
                    // set the conditions for a button style change.
                    var style = ShipManifestBehaviour.SelectedPartSource == part ? ManifestStyle.ButtonToggledSourceStyle : ManifestStyle.ButtonStyle;
                    if (GUILayout.Button(string.Format("{0}", part.partInfo.title), style, GUILayout.Width(265), GUILayout.Height(20)))
                    {
                        ShipManifestBehaviour.SelectedModuleSource = null;
                        ShipManifestBehaviour.SelectedPartSource = part;
                        ManifestUtilities.LogMessage("SelectedPartSource...", "Info", SettingsManager.VerboseLogging);
                    }
                }

                GUILayout.EndVertical();
                GUILayout.EndScrollView();
            }
            catch (Exception ex)
            {
                ManifestUtilities.LogMessage(" in Ship Manifest Window - SourceTransferViewer.  Error:  " + ex.ToString(), "Error", true);
            }
        }

        private void SourceDetailsViewer()
        {
            try
            {
                // Source Part resource Details
                // this Scroll viewer is for the details of the part selected above.
                SourceScrollViewerTransfer2 = GUILayout.BeginScrollView(SourceScrollViewerTransfer2, GUILayout.Height(90), GUILayout.Width(300));
                GUILayout.BeginVertical();

                if (ShipManifestBehaviour.SelectedPartSource != null)
                {
                    if (ShipManifestBehaviour.SelectedResource == "Crew")
                    {
                        List<ProtoCrewMember> crewMembers = ShipManifestBehaviour.SelectedPartSource.protoModuleCrew;
                        for (int x = 0; x < ShipManifestBehaviour.SelectedPartSource.protoModuleCrew.Count(); x++)
                        {
                            ProtoCrewMember crewMember = ShipManifestBehaviour.SelectedPartSource.protoModuleCrew[x];
                            GUILayout.BeginHorizontal();
                            if (crewMember.seat != null)
                            {
                                if (GUILayout.Button(">>", ManifestStyle.ButtonStyle, GUILayout.Width(15), GUILayout.Height(20)))
                                {
                                    MoveCrewMember(crewMember, ShipManifestBehaviour.SelectedPartSource);
                                }
                            }
                            GUILayout.Label(string.Format("  {0}", crewMember.name), GUILayout.Width(190), GUILayout.Height(20));
                            if (ShipManifestBehaviour.CanBeXferred(ShipManifestBehaviour.SelectedPartSource))
                            {
                                if (GUILayout.Button("Xfer", ManifestStyle.ButtonStyle, GUILayout.Width(50), GUILayout.Height(20)))
                                {
                                    TransferCrewMember(ShipManifestBehaviour.SelectedPartSource, ShipManifestBehaviour.SelectedPartTarget, crewMember);
                                }
                            }
                            GUILayout.EndHorizontal();
                        }
                    }
                    else if (ShipManifestBehaviour.SelectedResource == "Science")
                    {
                        foreach (PartModule pm in ShipManifestBehaviour.SelectedPartSource.Modules)
                        {
                            // Containers.
                            int scienceCount = 0;
                            int capacity = 0;
                            if (pm is ModuleScienceContainer)
                            {
                                scienceCount = ((ModuleScienceContainer)pm).GetScienceCount();
                                capacity = ((ModuleScienceContainer)pm).capacity;
                            }
                            else if (pm is ModuleScienceExperiment)
                            {
                                scienceCount = ((ModuleScienceExperiment)pm).GetScienceCount();
                                capacity = 1;
                            }

                            if (pm is ModuleScienceExperiment || pm is ModuleScienceContainer)
                            {
                                GUILayout.BeginHorizontal();
                                GUILayout.Label(string.Format("{0} - ({1})", pm.moduleName, scienceCount.ToString()), GUILayout.Width(205), GUILayout.Height(20));

                                // If we have target selected, it is not the same as the source, and there is science to xfer.
                                if ((ShipManifestBehaviour.SelectedModuleTarget != null && pm != ShipManifestBehaviour.SelectedModuleTarget) && scienceCount > 0)
                                {
                                    if (GUILayout.Button("Xfer", ManifestStyle.ButtonStyle, GUILayout.Width(50), GUILayout.Height(20)))
                                    {
                                        ShipManifestBehaviour.SelectedModuleSource = pm;
                                        TransferScience(ShipManifestBehaviour.SelectedModuleSource, ShipManifestBehaviour.SelectedModuleTarget);
                                        ShipManifestBehaviour.SelectedModuleSource = null;
                                    }
                                }
                                GUILayout.EndHorizontal();
                            }
                        }
                    }
                    else
                    {
                        // resources are left....
                        foreach (PartResource resource in ShipManifestBehaviour.SelectedPartSource.Resources)
                        {
                            if (resource.info.name == ShipManifestBehaviour.SelectedResource)
                            {
                                // This routine assumes that a resource has been selected on the Resource manifest window.
                                string flowtextS = "Off";
                                bool flowboolS = ShipManifestBehaviour.SelectedPartSource.Resources[ShipManifestBehaviour.SelectedResource].flowState;
                                if (flowboolS)
                                {
                                    flowtextS = "On";
                                }
                                else
                                {
                                    flowtextS = "Off";
                                }
                                PartResource.FlowMode flowmodeS = ShipManifestBehaviour.SelectedPartSource.Resources[ShipManifestBehaviour.SelectedResource].flowMode;

                                GUILayout.BeginHorizontal();
                                GUILayout.Label(string.Format("({0}/{1})", resource.amount.ToString("#######0.####"), resource.maxAmount.ToString("######0.####")), GUILayout.Width(175), GUILayout.Height(20));
                                GUILayout.Label(string.Format("{0}", flowtextS), GUILayout.Width(30), GUILayout.Height(20));
                                if (GUILayout.Button("Flow", GUILayout.Width(50), GUILayout.Height(20)))
                                {
                                    if (flowboolS)
                                    {
                                        ShipManifestBehaviour.SelectedPartSource.Resources[ShipManifestBehaviour.SelectedResource].flowState = false;
                                        flowtextS = "Off";
                                    }
                                    else
                                    {
                                        ShipManifestBehaviour.SelectedPartSource.Resources[ShipManifestBehaviour.SelectedResource].flowState = true;
                                        flowtextS = "On";
                                    }
                                }
                                GUILayout.EndHorizontal();
                                if ((ShipManifestBehaviour.SelectedPartTarget != null && ShipManifestBehaviour.SelectedPartSource != ShipManifestBehaviour.SelectedPartTarget) &&
                                    (ShipManifestBehaviour.SelectedPartSource.Resources[resource.info.name].amount > 0 && ShipManifestBehaviour.SelectedPartTarget.Resources[resource.info.name].amount < ShipManifestBehaviour.SelectedPartTarget.Resources[resource.info.name].maxAmount))
                                {
                                    if (!ShipManifestBehaviour.tXferOn)
                                    {
                                        // let's determine how much of a resource we can move to the target.
                                        double maxXferAmount = ShipManifestBehaviour.SelectedPartTarget.Resources[resource.info.name].maxAmount - ShipManifestBehaviour.SelectedPartTarget.Resources[resource.info.name].amount;
                                        if (maxXferAmount > ShipManifestBehaviour.SelectedPartSource.Resources[resource.info.name].amount)
                                        {
                                            maxXferAmount = ShipManifestBehaviour.SelectedPartSource.Resources[resource.info.name].amount;
                                        }

                                        // This is used to set the slider to the max amount by default.  
                                        // OnUpdate draws every frame, so we need a way to ignore this or the slider will stay at max
                                        // We set XferAmount to -1 when we set new source or target parts.
                                        if (sXferAmount == -1)
                                        {
                                            sXferAmount = (float)maxXferAmount;
                                        }

                                        // Left Details...
                                        GUILayout.BeginHorizontal();
                                        GUILayout.Label("Enter Xfer Amt:  ", GUILayout.Width(125));
                                        string strXferAmount = sXferAmount.ToString();
                                        sXferAmount = float.Parse(GUILayout.TextField(strXferAmount, 20, GUILayout.Width(80)));
                                        if (GUILayout.Button("Xfer", GUILayout.Width(50), GUILayout.Height(20)))
                                        {
                                            TransferResource(ShipManifestBehaviour.SelectedPartSource, ShipManifestBehaviour.SelectedPartTarget, (double)sXferAmount);
                                        }
                                        GUILayout.EndHorizontal();
                                        GUILayout.BeginHorizontal();
                                        GUILayout.Label("Xfer:  ", GUILayout.Width(50), GUILayout.Height(20));
                                        sXferAmount = GUILayout.HorizontalSlider(sXferAmount, 0, (float)maxXferAmount, GUILayout.Width(210));
                                        GUILayout.EndHorizontal();
                                    }
                                }
                            }
                        }
                    }
                }
                GUILayout.EndVertical();
                GUILayout.EndScrollView();
            }
            catch (Exception ex)
            {
                ManifestUtilities.LogMessage(" in Ship Manifest Window - SourceDetailsViewer.  Error:  " + ex.ToString(), "Error", true);
            }
        }

        private void TargetTransferViewer()
        {
            try
            {
                // target part list
                TargetScrollViewerTransfer = GUILayout.BeginScrollView(TargetScrollViewerTransfer, GUILayout.Height(120), GUILayout.Width(300));
                GUILayout.BeginVertical();

                foreach (Part part in ShipManifestBehaviour.SelectedResourceParts)
                {
                    var style = ShipManifestBehaviour.SelectedPartTarget == part ? ManifestStyle.ButtonToggledTargetStyle : ManifestStyle.ButtonStyle;
                    if (GUILayout.Button(string.Format("{0}", part.partInfo.title), style, GUILayout.Width(265), GUILayout.Height(20)))
                    {
                        ShipManifestBehaviour.SelectedPartTarget = part;
                        ManifestUtilities.LogMessage("SelectedPartTarget...", "Info", SettingsManager.VerboseLogging);
                    }
                }

                GUILayout.EndVertical();
                GUILayout.EndScrollView();
            }
            catch (Exception ex)
            {
                ManifestUtilities.LogMessage(" in Ship Manifest Window - TargetTransferViewer.  Error:  " + ex.ToString(), "Error", true);
            }
        }

        private void TargetDetailsViewer()
        {
            try
            {
                // Target Part resource details
                TargetScrollViewerTransfer2 = GUILayout.BeginScrollView(TargetScrollViewerTransfer2, GUILayout.Height(90), GUILayout.Width(300));
                GUILayout.BeginVertical();

                // --------------------------------------------------------------------------
                if (ShipManifestBehaviour.SelectedPartTarget != null)
                {
                    if (ShipManifestBehaviour.SelectedResource == "Crew")
                    {
                        foreach (ProtoCrewMember crewMember in ShipManifestBehaviour.SelectedPartTarget.protoModuleCrew)
                        {
                            // This routine assumes that a resource has been selected on the Resource manifest window.
                            GUILayout.BeginHorizontal();
                            if (crewMember.seat != null)
                            {
                                if (GUILayout.Button(">>", ManifestStyle.ButtonStyle, GUILayout.Width(15), GUILayout.Height(20)))
                                {
                                    MoveCrewMember(crewMember, ShipManifestBehaviour.SelectedPartTarget);
                                }
                            }
                            GUILayout.Label(string.Format("  {0}", crewMember.name), GUILayout.Width(190), GUILayout.Height(20));
                            if (ShipManifestBehaviour.CanBeXferred(ShipManifestBehaviour.SelectedPartTarget))
                            {
                                // set the conditions for a button style change.
                                if (GUILayout.Button("Xfer", ManifestStyle.ButtonStyle, GUILayout.Width(50), GUILayout.Height(20)))
                                {
                                    TransferCrewMember(ShipManifestBehaviour.SelectedPartTarget, ShipManifestBehaviour.SelectedPartSource, crewMember);
                                }
                            }
                            GUILayout.EndHorizontal();
                        }
                    }
                    else if (ShipManifestBehaviour.SelectedResource == "Science")
                    {
                        int count = 0;
                        foreach (PartModule tpm in ShipManifestBehaviour.SelectedPartTarget.Modules)
                        {
                            if (tpm is ModuleScienceContainer)
                                count += 1;
                        }

                        foreach (PartModule pm in ShipManifestBehaviour.SelectedPartTarget.Modules)
                        {
                            // Containers.
                            int scienceCount = 0;
                            if (pm is ModuleScienceContainer)
                            {
                                scienceCount = ((ModuleScienceContainer)pm).GetScienceCount();
                                GUILayout.BeginHorizontal();
                                GUILayout.Label(string.Format("{0} - ({1})", pm.moduleName, scienceCount.ToString()), GUILayout.Width(205), GUILayout.Height(20));
                                // set the conditions for a button style change.
                                bool ShowReceive = false;
                                if (pm == ShipManifestBehaviour.SelectedModuleTarget)
                                    ShowReceive = true;
                                else if (count == 1)
                                    ShowReceive = true;
                                ShipManifestBehaviour.SelectedModuleTarget = pm;
                                var style = ShowReceive ? ManifestStyle.ButtonToggledTargetStyle : ManifestStyle.ButtonStyle;
                                if (GUILayout.Button("Recv", style, GUILayout.Width(50), GUILayout.Height(20)))
                                {
                                    ShipManifestBehaviour.SelectedModuleTarget = pm;
                                }
                                GUILayout.EndHorizontal();
                            }
                        }
                    }
                    else
                    {
                        // Resources
                        foreach (PartResource resource in ShipManifestBehaviour.SelectedPartTarget.Resources)
                        {
                            if (resource.info.name == ShipManifestBehaviour.SelectedResource)
                            {
                                // This routine assumes that a resource has been selected on the Resource manifest window.
                                string flowtextT = "Off";
                                bool flowboolT = ShipManifestBehaviour.SelectedPartTarget.Resources[ShipManifestBehaviour.SelectedResource].flowState;
                                if (flowboolT)
                                {
                                    flowtextT = "On";
                                }
                                else
                                {
                                    flowtextT = "Off";
                                }
                                PartResource.FlowMode flowmodeT = resource.flowMode;

                                GUILayout.BeginHorizontal();
                                GUILayout.Label(string.Format("({0}/{1})", resource.amount.ToString("#######0.####"), resource.maxAmount.ToString("######0.####")), GUILayout.Width(175), GUILayout.Height(20));
                                GUILayout.Label(string.Format("{0}", flowtextT), GUILayout.Width(30), GUILayout.Height(20));
                                if (GUILayout.Button("Flow", ManifestStyle.ButtonStyle, GUILayout.Width(50), GUILayout.Height(20)))
                                {
                                    if (flowboolT)
                                    {
                                        ShipManifestBehaviour.SelectedPartTarget.Resources[ShipManifestBehaviour.SelectedResource].flowState = false;
                                        flowtextT = "Off";
                                    }
                                    else
                                    {
                                        ShipManifestBehaviour.SelectedPartTarget.Resources[ShipManifestBehaviour.SelectedResource].flowState = true;
                                        flowtextT = "On";
                                    }
                                }
                                GUILayout.EndHorizontal();
                                if ((ShipManifestBehaviour.SelectedPartSource != null && ShipManifestBehaviour.SelectedPartSource != ShipManifestBehaviour.SelectedPartTarget) && (ShipManifestBehaviour.SelectedPartTarget.Resources[resource.info.name].amount > 0 && ShipManifestBehaviour.SelectedPartSource.Resources[resource.info.name].amount < ShipManifestBehaviour.SelectedPartSource.Resources[resource.info.name].maxAmount))
                                {
                                    // create xfer slider;
                                    if (!ShipManifestBehaviour.sXferOn)
                                    {
                                        // let's determine how much of a resource we can move to the Source.
                                        double maxXferAmount = ShipManifestBehaviour.SelectedPartSource.Resources[resource.info.name].maxAmount - ShipManifestBehaviour.SelectedPartSource.Resources[resource.info.name].amount;
                                        if (maxXferAmount > ShipManifestBehaviour.SelectedPartTarget.Resources[resource.info.name].amount)
                                        {
                                            maxXferAmount = ShipManifestBehaviour.SelectedPartTarget.Resources[resource.info.name].amount;
                                        }

                                        // This is used to set the slider to the max amount by default.  
                                        // OnUpdate draws every frame, so we need a way to ignore this or the slider will stay at max
                                        // We set XferAmount to -1 when we set new source or target parts.
                                        if (tXferAmount == -1)
                                        {
                                            tXferAmount = (float)maxXferAmount;
                                        }

                                        GUILayout.BeginHorizontal();
                                        GUILayout.Label("Enter Xfer Amt:  ", GUILayout.Width(125));
                                        string strXferAmount = tXferAmount.ToString();
                                        tXferAmount = float.Parse(GUILayout.TextField(strXferAmount, 20, GUILayout.Width(80)));
                                        if (GUILayout.Button("Xfer", GUILayout.Width(50), GUILayout.Height(20)))
                                        {
                                            TransferResource(ShipManifestBehaviour.SelectedPartTarget, ShipManifestBehaviour.SelectedPartSource, (double)tXferAmount);
                                        }
                                        GUILayout.EndHorizontal();
                                        GUILayout.BeginHorizontal();
                                        GUILayout.Label("Xfer:  ", GUILayout.Width(50), GUILayout.Height(20));
                                        tXferAmount = GUILayout.HorizontalSlider(tXferAmount, 0, (float)maxXferAmount, GUILayout.Width(210));
                                        GUILayout.EndHorizontal();
                                    }
                                }
                            }
                        }
                    }
                }
                // --------------------------------------------------------------------------
                GUILayout.EndVertical();
                GUILayout.EndScrollView();
            }
            catch (Exception ex)
            {
                ManifestUtilities.LogMessage(" in Ship Manifest Window - TargetDetailsViewer.  Error:  " + ex.ToString(), "Error", true);
            }
        }

        #endregion

        #region Methods

        private void MoveCrewMember(ProtoCrewMember crewMember, Part part)
        {
            try
            {
                // Build source and target seat indexes.
                int curIdx = crewMember.seatIdx;
                int newIdx = curIdx;
                InternalSeat sourceSeat = crewMember.seat;
                if (newIdx + 1 == part.CrewCapacity)
                    newIdx = -1;

                // get target seat from part's inernal model
                InternalSeat targetSeat = part.internalModel.seats[newIdx + 1];

                // Do we need to swap places with another Kerbal?
                if (targetSeat.taken)
                {
                    // get Kerbal to swap with through his seat...
                    ProtoCrewMember targetMember = targetSeat.kerbalRef.protoCrewMember;

                    // Swap places.

                    // Remove the crew members from the part...
                    RemoveCrew(crewMember, part);
                    RemoveCrew(targetMember, part);

                    // At this point, the kerbals are in the "ether".
                    // this may be why there is an issue with refreshing the internal view.. 
                    // It may allow (or expect) a board call from an (invisible) eva object.   
                    // If I can manage to properly trigger that call... then all should properly refresh...
                    // I'll look into that...

                    // Update:  Thanks to Extraplanetary LaunchPads for helping me solve this problem!
                    ShipManifestBehaviour.evaAction = new GameEvents.FromToAction<Part, Part>(part, part);
                    GameEvents.onCrewOnEva.Fire(ShipManifestBehaviour.evaAction);
                    
                    // Add the crew members back into the part at their new seats.
                    part.AddCrewmemberAt(crewMember, newIdx + 1);
                    part.AddCrewmemberAt(targetMember, curIdx);
                }
                else
                {
                    // Just move.
                    RemoveCrew(crewMember, part);
                    ShipManifestBehaviour.evaAction = new GameEvents.FromToAction<Part, Part>(part, part);
                    GameEvents.onCrewOnEva.Fire(ShipManifestBehaviour.evaAction);
                    part.AddCrewmemberAt(crewMember, newIdx + 1);
                }

                ShipManifestBehaviour.isSeat2Seat = true;

                if (ShipManifestBehaviour.ShipManifestSettings.RealismMode)
                    ShipManifestBehaviour.crewXfer = true;
                else
                {
                    GameEvents.onCrewBoardVessel.Fire(ShipManifestBehaviour.evaAction);
                    GameEvents.onVesselChange.Fire(FlightGlobals.ActiveVessel);
                    ShipManifestBehaviour.clsVessel = CLSAddon.Instance.Vessel;

                    //if (ShipManifestBehaviour.clsVessel.Spaces != null && ShipManifestBehaviour.SelectedResource == "Crew")
                    //{
                    //    if (ShipManifestBehaviour.SelectedPartSource != null)
                    //    {
                    //        Part.OnActionDelegate OnMouseExit = ShipManifestBehaviour.MouseExit;
                    //        ShipManifestBehaviour.SelectedPartSource.RemoveOnMouseExit(OnMouseExit);
                    //    }
                    //    if (ShipManifestBehaviour.SelectedPartTarget != null)
                    //    {
                    //        Part.OnActionDelegate OnMouseExit = ShipManifestBehaviour.MouseExit;
                    //        ShipManifestBehaviour.SelectedPartSource.RemoveOnMouseExit(OnMouseExit);
                    //    }
                    //    foreach (CLSSpace space in ShipManifestBehaviour.clsVessel.Spaces)
                    //    {
                    //        space.Highlight(false);
                    //    }
                    //}
                    //ShipManifestBehaviour.OnMouseHighlights();
                }
            }
            catch (Exception ex)
            {
                ManifestUtilities.LogMessage("Error moving crewmember.  Error:  " + ex.ToString(), "Error", true);
            }
        }

        private void TransferCrewMember(Part source, Part target, ProtoCrewMember crewMember)
        {
            try
            {
                RemoveCrew(crewMember, source);

                // Support for Extraplanetary LaunchPads
                // This is the event trigger I was looking for to begin with on crew xfers!
               ShipManifestBehaviour.evaAction = new GameEvents.FromToAction<Part,Part>(source,target);
               GameEvents.onCrewOnEva.Fire(ShipManifestBehaviour.evaAction);
                crewMember.rosterStatus = ProtoCrewMember.RosterStatus.AVAILABLE;

                target.AddCrewmember(crewMember);
 
                if (crewMember.seat == null)
                    ManifestUtilities.LogMessage(string.Format("Crew member seat is null."), "Info", true);

                if (ShipManifestBehaviour.ShipManifestSettings.RealismMode)
                    ShipManifestBehaviour.crewXfer = true;
                else
                {
                    GameEvents.onCrewBoardVessel.Fire(ShipManifestBehaviour.evaAction);
                    GameEvents.onVesselChange.Fire(FlightGlobals.ActiveVessel);
                    ShipManifestBehaviour.OnMouseHighlights();
                }
               
            }
            catch (Exception ex)
            {
                ManifestUtilities.LogMessage("Error TransferCrewMember.  Error:  " + ex.ToString(), "Error", true);
            }
        }

        private void TransferScience(PartModule source, PartModule target)
        {
            ScienceData[] moduleScience = null;
            int i;
            try
            {
                if (source is ModuleScienceContainer)
                {
                    if (((ModuleScienceContainer)source) != null)
                        moduleScience = ((ModuleScienceContainer)source).GetData();
                    else
                        moduleScience = null;
                    //ManifestUtilities.LogMessage("Science Data Count:  " + ((ModuleScienceContainer) source).GetStoredDataCount(), "Info", SettingsManager.VerboseLogging);
                    //ManifestUtilities.LogMessage("Science Data s Collectable:  " + ((ModuleScienceContainer) source).dataIsCollectable, "Info", SettingsManager.VerboseLogging);
                }
                else
                {
                    if (((ModuleScienceExperiment)source) != null)
                    {
                        moduleScience = ((ModuleScienceExperiment)source).GetData();
                        ManifestUtilities.LogMessage("moduleScience is collected:  ", "Info", SettingsManager.VerboseLogging);
                    }
                    else
                    {
                        moduleScience = null;
                        ManifestUtilities.LogMessage("moduleScience is null:  ", "Info", SettingsManager.VerboseLogging);
                    }
                }

                if (moduleScience != null && moduleScience.Length > 0)
                {
                    for (i = 0; i < moduleScience.Length; i++)
                    {
                        ManifestUtilities.LogMessage(string.Format("moduleScience has data..."), "Info", SettingsManager.VerboseLogging);

                        if (((ModuleScienceContainer)target) != null)
                        {
                            if (((ModuleScienceContainer)target).AddData(moduleScience[i]))
                            {
                                if (source is ModuleScienceContainer)
                                {
                                    //((ModuleScienceContainer)source).
                                    ManifestUtilities.LogMessage(string.Format("((ModuleScienceContainer)source) is not null"), "Info", SettingsManager.VerboseLogging);

                                    ((ModuleScienceContainer)source).RemoveData(moduleScience[i]);
                                }
                                else
                                {
                                    if (ShipManifestBehaviour.ShipManifestSettings.RealismMode)
                                    {
                                        ManifestUtilities.LogMessage(string.Format("((Module ScienceExperiment xferred.  Dump Source data"), "Info", SettingsManager.VerboseLogging);
                                        ((ModuleScienceExperiment)source).DumpData(moduleScience[i]);
                                    }
                                    else
                                    {
                                        ManifestUtilities.LogMessage(string.Format("((Module ScienceExperiment xferred. Reset Experiment"), "Info", SettingsManager.VerboseLogging);
                                        ((ModuleScienceExperiment)source).ResetExperiment();
                                    }
                                }
                            }
                            else
                            {
                                ManifestUtilities.LogMessage(string.Format("Science Data transfer failed..."), "Info", true);
                            }
                        }
                        else
                        {
                            ManifestUtilities.LogMessage(string.Format("((ModuleScienceExperiment)target) is null"), "Info", true);
                        }
                    }
                    ManifestUtilities.LogMessage(string.Format("Transfer Complete."), "Info", SettingsManager.VerboseLogging);
                }
                else if (moduleScience == null)
                {
                    ManifestUtilities.LogMessage(string.Format("moduleScience is null..."), "Info", SettingsManager.VerboseLogging);
                }
                else  // must be length then...
                {
                    ManifestUtilities.LogMessage(string.Format("moduleScience empty (no data)..."), "Info", SettingsManager.VerboseLogging);
                }
            }
            catch (Exception ex)
            {
                ManifestUtilities.LogMessage(" in TransferScience:  Error:  " + ex.ToString(), "Info", true);
            }
        }

        private void TransferResource(Part source, Part target, double XferAmount)
        {
            try
            {
                if (source.Resources.Contains(ShipManifestBehaviour.SelectedResource) && target.Resources.Contains(ShipManifestBehaviour.SelectedResource))
                {
                    double maxAmount = target.Resources[ShipManifestBehaviour.SelectedResource].maxAmount;
                    double sourceAmount = source.Resources[ShipManifestBehaviour.SelectedResource].amount;
                    double targetAmount = target.Resources[ShipManifestBehaviour.SelectedResource].amount;
                    if (XferAmount == 0)
                    {
                        XferAmount = maxAmount - targetAmount;
                    }

                    // make sure we have enough...
                    if (XferAmount > sourceAmount)
                    {
                        XferAmount = sourceAmount;
                    }
                    if (ShipManifestBehaviour.ShipManifestSettings.RealismMode)
                    {
                        // now lets make some noise and slow the process down...
                        ManifestUtilities.LogMessage("Playing pump sound...", "Info", SettingsManager.VerboseLogging);


                        // This flag enables the Update handler in ResourceManifestBehaviour
                        if (source == ShipManifestBehaviour.SelectedPartSource)
                            ShipManifestBehaviour.sXferOn = true;
                        else
                            ShipManifestBehaviour.tXferOn = true;
                    }
                    else
                    {
                        // Fill target
                        target.Resources[ShipManifestBehaviour.SelectedResource].amount += XferAmount;

                        // Drain source...
                        source.Resources[ShipManifestBehaviour.SelectedResource].amount -= XferAmount;
                    }
                }
            }
            catch (Exception ex)
            {
                ManifestUtilities.LogMessage(" in  TransferResource.  Error:  " + ex.ToString(), "Error", true);
            }
        }

        private InternalSeat GetFreeSeat(List<InternalSeat> seats)
        {
            try
            {

                foreach (InternalSeat seat in seats)
                {
                    if (!seat.taken)
                    {
                        return seat;
                    }
                }
            }
            catch (Exception ex)
            {
                ManifestUtilities.LogMessage(" in  GetFreeSeat.  Error:  " + ex.ToString(), "Error", true);
            }
            return null;
        }

        private void logCrewMember(ProtoCrewMember crewMember, Part target)
        {
            try
            {
                ManifestUtilities.LogMessage(string.Format("Crew member:"), "Info", SettingsManager.VerboseLogging);
                ManifestUtilities.LogMessage(string.Format(" - Name:          " + crewMember.name), "Info", SettingsManager.VerboseLogging);
                ManifestUtilities.LogMessage(string.Format(" - KerbalRef:     " + (crewMember.KerbalRef != null).ToString()), "Info", SettingsManager.VerboseLogging);
                if (crewMember.KerbalRef != null)
                    ManifestUtilities.LogMessage(" - kerbalRef.Name:   " + crewMember.KerbalRef.name, "info", SettingsManager.VerboseLogging);
                ManifestUtilities.LogMessage(string.Format(" - rosterStatus:  " + crewMember.rosterStatus.ToString()), "Info", SettingsManager.VerboseLogging);
                ManifestUtilities.LogMessage(string.Format(" - seatIdx:       " + crewMember.seatIdx.ToString()), "Info", SettingsManager.VerboseLogging);
                ManifestUtilities.LogMessage(string.Format(" - seat != null:  " + (crewMember.seat != null).ToString()), "Info", SettingsManager.VerboseLogging);
                if (crewMember.seat != null)
                {
                    ManifestUtilities.LogMessage(string.Format(" - seat part name:     " + crewMember.seat.part.partInfo.name.ToString()), "Info", SettingsManager.VerboseLogging);
                    //ManifestUtilities.LogMessage(string.Format(" - portraitCamera:     " + crewMember.seat.portraitCamera.enabled.ToString()), "Info", SettingsManager.VerboseLogging);
                }
            }
            catch (Exception ex)
            {
                ManifestUtilities.LogMessage(string.Format("Error logging crewMember:  " + ex.ToString()), "Error", true);
            }
        }

        #endregion
    }
}