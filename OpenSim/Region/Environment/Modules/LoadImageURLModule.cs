/*
* Copyright (c) Contributors, http://opensimulator.org/
* See CONTRIBUTORS.TXT for a full list of copyright holders.
*
* Redistribution and use in source and binary forms, with or without
* modification, are permitted provided that the following conditions are met:
*     * Redistributions of source code must retain the above copyright
*       notice, this list of conditions and the following disclaimer.
*     * Redistributions in binary form must reproduce the above copyright
*       notice, this list of conditions and the following disclaimer in the
*       documentation and/or other materials provided with the distribution.
*     * Neither the name of the OpenSim Project nor the
*       names of its contributors may be used to endorse or promote products
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
* 
*/

using System;
using System.Drawing;
using System.IO;
using System.Net;
using libsecondlife;
using Nini.Config;
using OpenJPEGNet;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Environment.Modules
{
    public class LoadImageURLModule : IRegionModule, IDynamicTextureRender
    {
        private string m_name = "LoadImageURL";
        private IDynamicTextureManager m_textureManager;
        private Scene m_scene;

        public void Initialise(Scene scene, IConfigSource config)
        {
            if (m_scene == null)
            {
                m_scene = scene;
            }
        }

        public void PostInitialise()
        {
            m_textureManager = m_scene.RequestModuleInterface<IDynamicTextureManager>();
            m_textureManager.RegisterRender(GetContentType(), this);
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return m_name; }
        }

        public bool IsSharedModule
        {
            get { return true; }
        }

        public string GetName()
        {
            return m_name;
        }

        public string GetContentType()
        {
            return ("image");
        }

        public bool SupportsAsynchronous()
        {
            return true;
        }

        public byte[] ConvertUrl(string url, string extraParams)
        {
            return null;
        }

        public byte[] ConvertStream(Stream data, string extraParams)
        {
            return null;
        }

        public bool AsyncConvertUrl(LLUUID id, string url, string extraParams)
        {
            MakeHttpRequest(url, id);
            return true;
        }

        public bool AsyncConvertData(LLUUID id, string bodyData, string extraParams)
        {
            return false;
        }

        private void MakeHttpRequest(string url, LLUUID requestID)
        {
            WebRequest request = HttpWebRequest.Create(url);
            RequestState state = new RequestState((HttpWebRequest) request, requestID);
            IAsyncResult result = request.BeginGetResponse(new AsyncCallback(HttpRequestReturn), state);

            TimeSpan t = (DateTime.UtcNow - new DateTime(1970, 1, 1));
            state.TimeOfRequest = (int) t.TotalSeconds;
        }

        private void HttpRequestReturn(IAsyncResult result)
        {
            RequestState state = (RequestState) result.AsyncState;
            WebRequest request = (WebRequest) state.Request;
            HttpWebResponse response = (HttpWebResponse) request.EndGetResponse(result);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                Bitmap image = new Bitmap(response.GetResponseStream());
                Bitmap resize = new Bitmap(image, new Size(512, 512));
                byte[] imageJ2000 = OpenJPEG.EncodeFromImage(resize, true);

                m_textureManager.ReturnData(state.RequestID, imageJ2000);
            }
        }

        public class RequestState
        {
            public HttpWebRequest Request = null;
            public LLUUID RequestID = LLUUID.Zero;
            public int TimeOfRequest = 0;

            public RequestState(HttpWebRequest request, LLUUID requestID)
            {
                Request = request;
                RequestID = requestID;
            }
        }
    }
}