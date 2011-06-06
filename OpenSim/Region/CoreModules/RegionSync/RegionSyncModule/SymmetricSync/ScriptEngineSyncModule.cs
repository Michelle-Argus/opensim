﻿/* Copyright 2011 (c) Intel Corporation
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * The name of the copyright holder may not be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Client;
using OpenSim.Region.CoreModules.Framework.InterfaceCommander;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using log4net;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Mono.Addins;

namespace OpenSim.Region.CoreModules.RegionSync.RegionSyncModule
{
    
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "AttachmentsModule")]
    public class ScriptEngineSyncModule : INonSharedRegionModule, IDSGActorSyncModule
    {
        #region INonSharedRegionModule

        public void Initialise(IConfigSource config)
        {
            m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

            IConfig syncConfig = config.Configs["RegionSyncModule"];
            m_active = false;
            if (syncConfig == null)
            {
                m_log.Warn(LogHeader + " No RegionSyncModule config section found. Shutting down.");
                return;
            }
            else if (!syncConfig.GetBoolean("Enabled", false))
            {
                m_log.Warn(LogHeader + " RegionSyncModule is not enabled. Shutting down.");
                return;
            }

            string actorType = syncConfig.GetString("DSGActorType", "").ToLower();
            if (!actorType.Equals("script_engine"))
            {
                m_log.Warn(LogHeader + ": not configured as Scene Persistence Actor. Shut down.");
                return;
            }

            m_actorID = syncConfig.GetString("ActorID", "");
            if (m_actorID.Equals(""))
            {
                m_log.Warn(LogHeader + ": ActorID not specified in config file. Shutting down.");
                return;
            }

            m_active = true;

            LogHeader += "-" + m_actorID;
            m_log.Warn(LogHeader + " Initialised");

        }

        //Called after Initialise()
        public void AddRegion(Scene scene)
        {
            if (!m_active)
                return;
            m_log.Warn(LogHeader + " AddRegion() called");
            //connect with scene
            m_scene = scene;

            //register the module with SceneGraph. If needed, SceneGraph checks the module's ActorType to know what type of module it is.
            m_scene.RegisterModuleInterface<IDSGActorSyncModule>(this);

            // Setup the command line interface
            //m_scene.EventManager.OnPluginConsole += EventManager_OnPluginConsole;
            //InstallInterfaces();

            //Register for the OnPostSceneCreation event
            //m_scene.EventManager.OnPostSceneCreation += OnPostSceneCreation;

            //Register for Scene/SceneGraph events
            //m_scene.SceneGraph.OnObjectCreate += new ObjectCreateDelegate(ScriptEngine_OnObjectCreate);
            m_scene.SceneGraph.OnObjectCreateBySync += new ObjectCreateBySyncDelegate(ScriptEngine_OnObjectCreateBySync);
            m_scene.EventManager.OnSymmetricSyncStop += ScriptEngine_OnSymmetricSyncStop;

            //for local OnUpdateScript, we'll handle it the same way as a remove OnUpdateScript. 
            //RegionSyncModule will capture a locally initiated OnUpdateScript event and publish it to other actors.
            m_scene.EventManager.OnNewScript += ScriptEngine_OnNewScript;
            m_scene.EventManager.OnUpdateScript += ScriptEngine_OnUpdateScript;

            m_scene.EventManager.OnAggregateScriptEvents += ScriptEngine_OnAggregateScriptEvents;

            LogHeader += "-" + m_actorID + "-" + m_scene.RegionInfo.RegionName;
        }

        //Called after AddRegion() has been called for all region modules of the scene.
        //NOTE::However, at this point, Scene may not have requested all the needed region module interfaces yet.
        public void RegionLoaded(Scene scene)
        {
            if (!m_active)
                return;

        }

        public void RemoveRegion(Scene scene)
        {
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void Close()
        {
            m_scene = null;
        }

        public string Name
        {
            get { return "ScriptEngineSyncModule"; }
        }

        #endregion //INonSharedRegionModule

        #region IDSGActorSyncModule members and functions

        public static string ActorTypeString = DSGActorTypes.ScriptEngine.ToString();

        private DSGActorTypes m_actorType = DSGActorTypes.ScriptEngine;
        public DSGActorTypes ActorType
        {
            get { return m_actorType; }
        }

        private string m_actorID;
        public string ActorID
        {
            get { return m_actorID; }
        }

        #endregion //IDSGActorSyncModule


        #region ScriptEngineSyncModule memebers and functions
        private ILog m_log;
        private bool m_active = false;
        public bool Active
        {
            get { return m_active; }
        }

        private Scene m_scene;

        private string LogHeader = "[ScriptEngineSyncModule]";

        public void OnPostSceneCreation(Scene createdScene)
        {
            //If this is the local scene the actor is working on, do something
            if (createdScene == m_scene)
            {
            }
        }

        /// <summary>
        /// Script Engine's action upon an object is added to the local scene
        /// </summary>
        private void ScriptEngine_OnObjectCreateBySync(EntityBase entity)
        {
            if (entity is SceneObjectGroup)
            {
                //m_log.DebugFormat("{0}: start script for obj {1}", LogHeader, entity.UUID);
                SceneObjectGroup sog = (SceneObjectGroup)entity; 
                sog.CreateScriptInstances(0, false, m_scene.DefaultScriptEngine, 0);
                sog.ResumeScripts();
            }
        }

        public void ScriptEngine_OnSymmetricSyncStop()
        {
            //Inform script engine to save script states and stop scripts
            m_scene.EventManager.TriggerScriptEngineSyncStop();
            //remove all objects
            m_scene.DeleteAllSceneObjectsBySync();
        }

        public void ScriptEngine_OnNewScript(UUID agentID, SceneObjectPart part, UUID itemID)
        {
            m_log.Debug(LogHeader + " ScriptEngine_OnUpdateScript");

            ArrayList errors = m_scene.SymSync_OnNewScript(agentID, itemID, part);
            //The errors should be sent back to the client's viewer who submitted 
            //the new script. But for now, let just display it in concole and 
            //log it.
            LogScriptErrors(errors);
        }

        //Assumption, when this function is triggered, the new script asset has already been saved.
        public void ScriptEngine_OnUpdateScript(UUID agentID, UUID itemID, UUID primID, bool isScriptRunning, UUID newAssetID)
        {
            m_log.Debug(LogHeader + " ScriptEngine_OnUpdateScript");
            ArrayList errors = m_scene.SymSync_OnUpdateScript(agentID, itemID, primID, isScriptRunning, newAssetID);

            //The errors should be sent back to the client's viewer who submitted 
            //the script update. But for now, let just display it in concole and 
            //log it.
            LogScriptErrors(errors);
        }

        private void LogScriptErrors(ArrayList errors)
        {
            string errorString = "";
            foreach (Object err in errors)
            {
                errorString += err + "\n";
            }
            if (errorString != String.Empty)
            {
                m_log.ErrorFormat("Error in script: {0}", errorString);
            }
        }

        public void ScriptEngine_OnAggregateScriptEvents(SceneObjectPart part)
        {
            part.aggregateScriptEvents();
        }

        #endregion //ScriptEngineSyncModule

    }

}