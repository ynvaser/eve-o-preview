﻿using EveOPreview.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace EveOPreview.Services.Implementation
{
	sealed class ProcessMonitor : IProcessMonitor
	{
		#region Private constants
		private const string DEFAULT_PROCESS_NAME = "ExeFile";
		private const string CURRENT_PROCESS_NAME = "EVE-O Preview";
		#endregion

		#region Private fields
		private readonly IDictionary<IntPtr, string> _processCache;
		private IProcessInfo _currentProcessInfo;
		#endregion

		private IThumbnailConfiguration configuration;

		public ProcessMonitor(IThumbnailConfiguration configuration)
		{
			this.configuration = configuration;
			this._processCache = new Dictionary<IntPtr, string>(512);
			
			// This field cannot be initialized properly in constructor
			// At the moment this code is executed the main application window is not yet initialized
			this._currentProcessInfo = new ProcessInfo(IntPtr.Zero, "");
		}

		private bool IsMonitoredProcess(string processName)
		{
            return configuration.Processes.Contains(processName, StringComparer.OrdinalIgnoreCase);
		}

		private IProcessInfo GetCurrentProcessInfo()
		{
			var currentProcess = Process.GetCurrentProcess();
			return new ProcessInfo(currentProcess.MainWindowHandle, currentProcess.MainWindowTitle);
		}

		public IProcessInfo GetMainProcess()
		{
			if (this._currentProcessInfo.Handle == IntPtr.Zero)
			{
				var processInfo = this.GetCurrentProcessInfo();

				// Are we initialized yet?
				if (processInfo.Title != "")
				{
					this._currentProcessInfo = processInfo;
				}
			}

			return this._currentProcessInfo;
		}

		public ICollection<IProcessInfo> GetAllProcesses()
		{
			ICollection<IProcessInfo> result = new List<IProcessInfo>(this._processCache.Count);

			// TODO Lock list here just in case
			foreach (KeyValuePair<IntPtr, string> entry in this._processCache)
			{
				result.Add(new ProcessInfo(entry.Key, entry.Value));
			}

			return result;
		}

		public void GetUpdatedProcesses(out ICollection<IProcessInfo> addedProcesses, out ICollection<IProcessInfo> updatedProcesses, out ICollection<IProcessInfo> removedProcesses)
		{
			addedProcesses = new List<IProcessInfo>(16);
			updatedProcesses = new List<IProcessInfo>(16);
			removedProcesses = new List<IProcessInfo>(16);

			IList<IntPtr> knownProcesses = new List<IntPtr>(this._processCache.Keys);
			foreach (Process process in Process.GetProcesses())
			{
				string processName = process.ProcessName;

				if (!this.IsMonitoredProcess(processName))
				{
					continue;
				}

				IntPtr mainWindowHandle = process.MainWindowHandle;
				if (mainWindowHandle == IntPtr.Zero)
				{
					continue; // No need to monitor non-visual processes
				}

				string mainWindowTitle = process.MainWindowTitle;
				this._processCache.TryGetValue(mainWindowHandle, out string cachedTitle);

				if (cachedTitle == null)
				{
					// This is a new process in the list
					this._processCache.Add(mainWindowHandle, mainWindowTitle);
					addedProcesses.Add(new ProcessInfo(mainWindowHandle, mainWindowTitle));
				}
				else
				{
					// This is an already known process
					if (cachedTitle != mainWindowTitle)
					{
						this._processCache[mainWindowHandle] = mainWindowTitle;
						updatedProcesses.Add(new ProcessInfo(mainWindowHandle, mainWindowTitle));
					}

					knownProcesses.Remove(mainWindowHandle);
				}
			}

			foreach (IntPtr index in knownProcesses)
			{
				string title = this._processCache[index];
				removedProcesses.Add(new ProcessInfo(index, title));
				this._processCache.Remove(index);
			}
		}
	}
}
