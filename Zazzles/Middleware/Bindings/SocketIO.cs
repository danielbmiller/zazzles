﻿/*
 * Zazzles : A cross platform service framework
 * Copyright (C) 2014-2015 FOG Project
 * 
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version 3
 * of the License, or (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 */

using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Quobject.SocketIoClientDotNet.Client;


namespace Zazzles.Middleware.Bindings
{
    class SocketIO : IServerBinding
    {
        private static string LogName = "Middleware::Bindings::SocketIO";
        private Socket socket;

        public bool Bind()
        {
            var url = Configuration.ServerAddress + "/socketio";
            if (!InitiateSocket(url)) return false;
            Bus.Subscribe(Bus.Channel.RemoteTX, OnTXRequest);

            return true;
        }

        public bool UnBind()
        {
            socket?.Disconnect();
            Bus.Unsubscribe(Bus.Channel.RemoteTX, OnTXRequest);
            return true;
        }

        private bool InitiateSocket(string url)
        {
            try
            {
                socket = IO.Socket(url);

                socket.On("data", (data) =>
                {
                    try
                    {
                        dynamic jData = JObject.Parse(data.ToString());

                        if (jData.encryptedData != null)
                        {
                            var decryptedData = Authentication.Decrypt(jData.encryptedData);
                            jData = JObject.Parse(decryptedData);
                        }

                        var global = true;

                        if (jData.rootOnly != null && jData.rootOnly == true)
                        {
                            global = false;
                        }

                        Bus.Emit(Bus.Channel.RemoteRX, jData, global);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(LogName, "Failed to parse socket message");
                        Log.Error(LogName, ex);
                    }
                });

                return true;
            }
            catch (Exception ex)
            {
                Log.Error(LogName, "Failed to bind");
                Log.Error(LogName, ex);
            }

            return false;
        }

        private void OnTXRequest(dynamic data)
        {
            try
            {
                var serialized = JsonConvert.SerializeObject(data);
                // TODO: Encrypt message
                // serialized = Authentication.Encrypt(serialized);

                socket.Emit(serialized);
            }
            catch (Exception ex)
            {
                Log.Error(LogName, "Failed to transmit RemoteTX data");
                Log.Error(LogName, ex);
            }
        }
    }
}
