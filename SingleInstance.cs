#region License
/*
 * The MIT License (MIT)
 *
 * Copyright (c) 2016 Valentin Cocaud
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */
#endregion

using System;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Threading;

namespace SingleInstanceManager
{
    /// <summary>
    /// A manager for single instance application.
    /// </summary>
    /// <typeparam name="AssemblyClass">A classe from the single instance assembly</typeparam>
    public class SingleInstance<AssemblyClass>
    {
        #region Event Definition
        public delegate void NewInstanceEventHandler();
        public delegate void NewInstanceEventWithMessageHandler(string message);

        /// <summary>
        /// Raised when a new instance of the assembly has been created.
        /// This doesn't mean that the newly created instance will be shutdown.
        /// </summary>
        public event NewInstanceEventHandler NewInstance;

        /// <summary>
        /// Raised when a new instance of the assembly has been created with a message.
        /// A NewInstance event is always raised before this one.
        /// This doesn't mean that the newly created instance will be shutdown.
        /// </summary>
        public event NewInstanceEventWithMessageHandler NewInstanceWithMessage;
        #endregion

        #region Singleton Definition
        private static SingleInstance<AssemblyClass> singleton;

        /// <summary>
        /// Return the instance of the SingleInstance manager for the given class assembly.
        /// </summary>
        public static SingleInstance<AssemblyClass> Singleton
        {
            get { return singleton == null ? singleton = new SingleInstance<AssemblyClass>() : singleton; }
        }
        #endregion

        private Mutex mutex;
        private string appGUID;

        private SingleInstance()
        {
            appGUID = ((GuidAttribute)typeof(AssemblyClass).Assembly.GetCustomAttributes(typeof(GuidAttribute), true)[0]).Value;
            Console.WriteLine(appGUID);
            mutex = new Mutex(true, appGUID + "Mutex");
            GC.KeepAlive(mutex);
        }

        #region IsThereAnotherInstance
        /// <summary>
        /// Check for a living instance of the assembly.
        /// If an instance is found, it is notified that a new instance have been created without any message.
        /// </summary>
        /// <param name="exitOnOhterInstanceFound">If true, exit the application when an instance is found.</param>
        /// <returns>True if an instance is found</returns>
        public bool isThereAnotherInstance(bool exitOnOhterInstanceFound)
        {
            return isThereAnotherInstance(exitOnOhterInstanceFound, null);
        }

        /// <summary>
        /// Check for a living instance of the assembly.
        /// If an instance is found, it is notified that a new instance has been created with the given message.
        /// Exit the application if an instance is found
        /// </summary>
        /// <param name="message">The message that will be sent to the found instance if one</param>
        /// <returns>True if an instance is found</returns>
        public bool isThereAnotherInstance(string message)
        {
            return isThereAnotherInstance(true, message);
        }

        /// <summary>
        /// Check for a living instance of the assembly.
        /// If an instance is found, it is notified that a new instance has been created without any message.
        /// Exit the application if an instance is found
        /// </summary>
        /// <returns>True if an instance is found</returns>
        public bool isThereAnotherInstance()
        {
            return isThereAnotherInstance(true);
        }

        /// <summary>
        /// Check for a living instance of the assembly.
        /// If an instance is found, it is notified that a new instance has been created with the given message.
        /// </summary>
        /// <param name="message">The message that will be sent to the found instance if one</param>
        /// <param name="exitOnOhterInstanceFound">If true, exit the application if a instance is found</param>
        /// <returns>True if an instance is found</returns>
        public bool isThereAnotherInstance(bool exitOnOhterInstanceFound, string message)
        {
            if (mutex.WaitOne(TimeSpan.Zero, true)) return false;

            Console.WriteLine("The application is already running.");

            NamedPipeClientStream stream = new NamedPipeClientStream(".", appGUID + "Pipe", PipeDirection.Out);
            stream.Connect(100);

            if (message != null)
            {
                StreamWriter writerStream = new StreamWriter(stream);
                writerStream.Write(message);
                writerStream.Close();
            }

            stream.Close();

            if (exitOnOhterInstanceFound) Environment.Exit(0);
            return true;
        }
        #endregion

        #region WaitForOtherInstances
        public void WaitForOtherInstances()
        {
            while (true)
            {
                var stream = new NamedPipeServerStream(appGUID + "Pipe", PipeDirection.In);
                stream.WaitForConnection();

                StreamReader streamReader = new StreamReader(stream);
                string message = streamReader.ReadToEnd();
                streamReader.Close();

                NewInstance?.Invoke();
                if (message != null && message.Length != 0) NewInstanceWithMessage?.Invoke(message);

                stream.Close();
            }
        }
        public void WaitForOtherInstances(bool runInBackground)
        {
            if (runInBackground)
            {
                Thread thread = new Thread(new ThreadStart(WaitForOtherInstances));
                thread.Start();
            }
            else WaitForOtherInstances();
        }
        #endregion
    }
}
