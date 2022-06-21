/*
 * Licensed to De Bitmanager under one or more contributor
 * license agreements. See the NOTICE file distributed with
 * this work for additional information regarding copyright
 * ownership. De Bitmanager licenses this file to you under
 * the Apache License, Version 2.0 (the "License"); you may
 * not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *    http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing,
 * software distributed under the License is distributed on an
 * "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
 * KIND, either express or implied.  See the License for the
 * specific language governing permissions and limitations
 * under the License.
 */

using Bitmanager.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Bitmanager.BigFile {
   public static class Globals {
      public const String TITLE = "BigFile";
      public static readonly Logger MainLogger;
      public static readonly Logger SettingsLogger;
      public static readonly Logger TooltipLogger;

      public static readonly String LoadDir;
      public static readonly bool IsDebug;

      public static readonly String UCoreDll;
      public static readonly String UCoreDllVersion;
      public static readonly bool CanCompress;
      public static readonly bool CanInternalGZip;

      static Globals () {
         MainLogger = Logs.CreateLogger ("bigfile", "main");
         MainLogger.Log ();
         SettingsLogger = MainLogger.Clone ("settings");
         TooltipLogger = Globals.MainLogger.Clone ("tt");


         LoadDir = Path.GetDirectoryName (Assembly.GetExecutingAssembly ().Location);
         IsDebug = File.Exists (Path.Combine (LoadDir, "debug.txt"));

         UCoreDll = Environment.Is64BitProcess ? "bmucore102_64" : "bmucore102_32";
         Version v = BMVersion.FromDll (UCoreDll);
         UCoreDllVersion = v == null ? null : v.ToString ();
         CanCompress = BMVersion.HasMinimalVersion (v, 1, 2, 2019, 429);
         CanInternalGZip = BMVersion.HasMinimalVersion (v, 1, 2, 2020, 424);
         MainLogger.Log ();
         MainLogger.Log ("Cmdline=" + Environment.CommandLine);
         MainLogger.Log ("UCore version: {0}, Can GZip: {1}", UCoreDllVersion, CanInternalGZip);
      }

   }
}
