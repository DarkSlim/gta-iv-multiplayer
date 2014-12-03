﻿using System.Collections.Generic;

namespace MIVClient
{
    public class ChatController
    {
        public Queue<string> chatconsole;
        public Queue<string> debugconsole;
        public string currentTypedText;

        private Client client;

        public ChatController(Client client)
        {
            this.client = client;
            currentTypedText = "";
            chatconsole = new Queue<string>();
            debugconsole = new Queue<string>();
            writeChat("Warning: This is alpha software.");
            writeChat("MIV 0.1 - Press L to connect.");
        }

        public void writeChat(string text)
        {
            chatconsole.Enqueue(text);
            while (chatconsole.Count > 12)
            {
                chatconsole.Dequeue();
            }
        }
        public void writeDebug(string text)
        {
            debugconsole.Enqueue(text);
            while (debugconsole.Count > 40)
            {
                debugconsole.Dequeue();
            }
        }
    }
}