﻿using GTA;
using MIVSDK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace MIVClient
{
    public class ServerConnection
    {
        private Client client;
        private byte[] buffer;
        public Dictionary<byte, UpdateDataStruct> playersdata;

        public ServerConnection(Client client)
        {
            this.client = client;
            buffer = new byte[1024 * 1024];
            playersdata = new Dictionary<byte, UpdateDataStruct>();
            streamBegin();
            client.client.Client.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, onReceive, null);
        }

        private void onReceive(IAsyncResult iar)
        {
            try
            {
                client.client.Client.EndReceive(iar);
                if (iar.IsCompleted)
                {
                    if (buffer.Length > 0)
                    {
                        switch ((MIVSDK.Commands)BitConverter.ToUInt16(buffer, 0))
                        {
                            case MIVSDK.Commands.UpdateData:
                                {
                                    byte playerid = buffer[2];
                                    MIVSDK.UpdateDataStruct data = MIVSDK.UpdateDataStruct.unserialize(buffer, 3);
                                    if (!playersdata.ContainsKey(playerid)) playersdata.Add(playerid, data);
                                    else playersdata[playerid] = data;
                                }
                                break;

                            case MIVSDK.Commands.Chat_writeLine:
                                {
                                    var list = buffer.ToList();
                                    int lineLength = BitConverter.ToInt32(buffer, 2);
                                    string line = Encoding.UTF8.GetString(list.Skip(2 + 4).Take(lineLength).ToArray());
                                    client.chatController.writeChat(line);
                                }
                                break;

                            case MIVSDK.Commands.Player_setPosition:
                                {
                                    float x = BitConverter.ToSingle(buffer, 2);
                                    float y = BitConverter.ToSingle(buffer, 6);
                                    float z = BitConverter.ToSingle(buffer, 10);
                                    client.enqueueAction(new Action(delegate
                                    {
                                        client.getPlayerPed().Position = new Vector3(x, y, z);
                                    }));
                                }
                                break;

                            case MIVSDK.Commands.Vehicle_create_multi:
                                {
                                    // uint id, int model, float x y z, rx, ry, rz, rw, vx, vy, vz
                                    int offset = 2;
                                    int count = BitConverter.ToInt32(buffer, offset); offset += 4;
                                    int outlen = 0;
                                    for (int i = 0; i < count; i++)
                                    {
                                        uint id = BitConverter.ToUInt32(buffer, offset); offset += 4;
                                        float x = BitConverter.ToSingle(buffer, offset); offset += 4;
                                        float y = BitConverter.ToSingle(buffer, offset); offset += 4;
                                        float z = BitConverter.ToSingle(buffer, offset); offset += 4;
                                        float rx = BitConverter.ToSingle(buffer, offset); offset += 4;
                                        float ry = BitConverter.ToSingle(buffer, offset); offset += 4;
                                        float rz = BitConverter.ToSingle(buffer, offset); offset += 4;
                                        float rw = BitConverter.ToSingle(buffer, offset); offset += 4;
                                        float vx = BitConverter.ToSingle(buffer, offset); offset += 4;
                                        float vy = BitConverter.ToSingle(buffer, offset); offset += 4;
                                        float vz = BitConverter.ToSingle(buffer, offset); offset += 4;
                                        string model = Serializers.unserialize_string(buffer, offset, out outlen); offset += outlen;
                                        client.enqueueAction(new Action(delegate
                                        {
                                            client.vehicleController.create(id, model, new Vector3(x, y, z), new Quaternion(rx, ry, rz, rw), new Vector3(vx, vy, vz));
                                        }));
                                    }
                                }
                                break;
                            case MIVSDK.Commands.NPC_create:
                                {
                                    // uint id, int model, float x y z, rx, ry, rz, rw, vx, vy, vz
                                    int offset = 2;
                                    int outlen = 0;
                                    uint id = BitConverter.ToUInt32(buffer, offset); offset += 4;
                                    float x = BitConverter.ToSingle(buffer, offset); offset += 4;
                                    float y = BitConverter.ToSingle(buffer, offset); offset += 4;
                                    float z = BitConverter.ToSingle(buffer, offset); offset += 4;
                                    float heading = BitConverter.ToSingle(buffer, offset); offset += 4;

                                    string modelname = Serializers.unserialize_string(buffer, offset, out outlen); offset += outlen;
                                    string[] split = modelname.Split(';');
                                    client.enqueueAction(new Action(delegate
                                    {
                                        client.npcPedController.getById(id, split[0], split[1], heading, new Vector3(x, y, z));
                                    }));

                                }
                                break;
                            case MIVSDK.Commands.NPCDialog_show:
                                {
                                    // uint id, int model, float x y z, rx, ry, rz, rw, vx, vy, vz
                                    int offset = 2;
                                    uint id = BitConverter.ToUInt32(buffer, offset); offset += 4;
                                    string str = Serializers.unserialize_string(buffer, offset);
                                    string[] split = str.Split('\x01');
                                    foreach (string s in split) Game.Console.Print(s);
                                    client.enqueueAction(new Action(delegate
                                    {
                                        GTA.Forms.Form form = new GTA.Forms.Form();

                                        GTA.Forms.Label caption = new GTA.Forms.Label();
                                        caption.Location = new System.Drawing.Point(10, 10);
                                        caption.Text = split[0];

                                        GTA.Forms.Label text = new GTA.Forms.Label();
                                        text.Location = new System.Drawing.Point(10, 40);
                                        text.Text = split[1];

                                        form.Controls.Add(caption);
                                        form.Controls.Add(text);

                                        for (int i = 2; i < split.Length; i++)
                                        {
                                            GTA.Forms.Button button = new GTA.Forms.Button();
                                            button.Location = new System.Drawing.Point(10, 40 + i*20);
                                            button.Text = split[i];

                                            button.MouseDown += (s, o) =>
                                            {
                                                streamWrite(Commands.NPCDialog_sendResponse);
                                                streamWrite(BitConverter.GetBytes(id));
                                                streamWrite(new byte[1] { (byte)(i - 2) });
                                                streamFlush();
                                                form.Close();
                                            };

                                            form.Controls.Add(button);
                                        }
                                        form.Show();
                                    }));

                                }
                                break;
                        }
                    }
                }
                buffer = new byte[1024 * 1024];
                client.client.Client.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, onReceive, null);
            }
            catch (Exception e)
            {
                //client.log("Failed receive with message " + e.Message);
                client.currentState = ClientState.Disconnected;
                //throw e;
            }
        }

        private byte[] appendBytes(byte[] byte1, byte[] byte2)
        {
            var list = byte1.ToList();
            list.AddRange(byte2);
            return list.ToArray();
        }

        private byte[] tempbuf;

        public void streamBegin()
        {
            tempbuf = new byte[0];
        }

        public void streamFlush()
        {
            client.client.Client.Send(tempbuf, tempbuf.Length, SocketFlags.None);
            streamBegin();
        }

        public void streamWrite(byte[] buffer)
        {
            tempbuf = appendBytes(tempbuf, buffer);
        }

        public void streamWrite(List<byte> buffer)
        {
            tempbuf = appendBytes(tempbuf, buffer.ToArray());
        }

        public void streamWrite(string buffer)
        {
            byte[] buf = Encoding.UTF8.GetBytes(buffer);
            tempbuf = appendBytes(tempbuf, buf);
        }

        public void streamWrite(UpdateDataStruct buffer)
        {
            byte[] buf = buffer.serialize();
            tempbuf = appendBytes(tempbuf, buf);
        }

        public void streamWrite(Commands command)
        {
            byte[] buf = BitConverter.GetBytes((ushort)command);
            tempbuf = appendBytes(tempbuf, buf);
        }

        public void streamWrite(int integer)
        {
            byte[] buf = BitConverter.GetBytes(integer);
            tempbuf = appendBytes(tempbuf, buf);
        }
    }
}