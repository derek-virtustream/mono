//
// System.Web.Configuration.WebConfigurationHost.cs
//
// Authors:
//  Lluis Sanchez Gual (lluis@novell.com)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
// Copyright (C) 2005 Novell, Inc (http://www.novell.com)
//

#if NET_2_0

using System;
using System.IO;
using System.Security;
using System.Configuration;
using System.Configuration.Internal;
using System.Web.Util;

/*
 * this class needs to be rewritten to support usage of the
 * IRemoteWebConfigurationHostServer interface.  Once that's done, we
 * need an implementation of that interface that talks (through a web
 * service?) to a remote site..
 *
 * for now, though, just implement it as we do
 * System.Configuration.InternalConfigurationHost, i.e. the local
 * case.
 */
namespace System.Web.Configuration
{
	class WebConfigurationHost: IInternalConfigHost
	{
		WebConfigurationFileMap map;
		const string MachinePath = ":machine:";
		const string MachineWebPath = ":web:";
		
		public virtual object CreateConfigurationContext (string configPath, string locationSubPath)
		{
			return new WebContext (WebApplicationLevel.AtApplication /* XXX */,
					       "" /* site XXX */,
					       "" /* application path XXX */,
					       configPath,
					       locationSubPath);
		}
		
		public virtual object CreateDeprecatedConfigContext (string configPath)
		{
			throw new NotImplementedException ();
		}
		
		public virtual string DecryptSection (string encryptedXml, ProtectedConfigurationProvider protectionProvider, ProtectedConfigurationSection protectedSection)
		{
			throw new NotImplementedException ();
		}
		
		public virtual void DeleteStream (string streamName)
		{
			File.Delete (streamName);
		}
		
		public virtual string EncryptSection (string encryptedXml, ProtectedConfigurationProvider protectionProvider, ProtectedConfigurationSection protectedSection)
		{
			throw new NotImplementedException ();
		}
		
		public virtual string GetConfigPathFromLocationSubPath (string configPath, string locatinSubPath)
		{
			return configPath + "/" + locatinSubPath;
		}
		
		public virtual Type GetConfigType (string typeName, bool throwOnError)
		{
			Type type = Type.GetType (typeName);
			if (type == null && throwOnError)
				throw new ConfigurationErrorsException ("Type not found: '" + typeName + "'");
			return type;
		}
		
		public virtual string GetConfigTypeName (Type t)
		{
			return t.AssemblyQualifiedName;
		}
		
		public virtual void GetRestrictedPermissions (IInternalConfigRecord configRecord, out PermissionSet permissionSet, out bool isHostReady)
		{
			throw new NotImplementedException ();
		}
		
		public virtual string GetStreamName (string configPath)
		{
			if (configPath == MachinePath) {
				if (map == null)
					return System.Runtime.InteropServices.RuntimeEnvironment.SystemConfigurationFile;
				else
					return map.MachineConfigFilename;
			} else if (configPath == MachineWebPath) {
				string mdir;

				if (map == null)
					mdir = Path.GetDirectoryName (System.Runtime.InteropServices.RuntimeEnvironment.SystemConfigurationFile);
				else
					mdir = Path.GetDirectoryName (map.MachineConfigFilename);

				return GetWebConfigFileName (mdir);
			}
			
			string dir = MapPath (configPath);
			return GetWebConfigFileName (dir);
		}
		
		public virtual string GetStreamNameForConfigSource (string streamName, string configSource)
		{
			throw new NotImplementedException ();
		}
		
		public virtual object GetStreamVersion (string streamName)
		{
			throw new NotImplementedException ();
		}
		
		public virtual IDisposable Impersonate ()
		{
			throw new NotImplementedException ();
		}
		
		public virtual void Init (IInternalConfigRoot root, params object[] hostInitParams)
		{
		}
		
		public virtual void InitForConfiguration (ref string locationSubPath, out string configPath, out string locationConfigPath, IInternalConfigRoot root, params object[] hostInitConfigurationParams)
		{
			string fullPath = (string) hostInitConfigurationParams [1];
			
			map = (WebConfigurationFileMap) hostInitConfigurationParams [0];
			
			if (locationSubPath == MachineWebPath) {
				locationSubPath = MachinePath;
				configPath = MachineWebPath;
				locationConfigPath = null;
			}
			else if (locationSubPath == MachinePath) {
				locationSubPath = null;
				configPath = MachinePath;
				locationConfigPath = null;
			}
			else {
				
				int i;
				if (locationSubPath == null)
					configPath = fullPath;
				else
					configPath = locationSubPath;

				string basedir = null;
				if (HttpContext.Current != null
				    && HttpContext.Current.Request != null)
					basedir = HttpContext.Current.Request.ApplicationPath;

				if (basedir == null)
					basedir = "/";

				if (configPath == basedir)
					i = -1;
				else
					i = configPath.LastIndexOf ("/");
				
				if (i != -1) {
					locationConfigPath = configPath.Substring (i+1);
					
					if (i == 0)
						locationSubPath = "/";
					else
						locationSubPath = fullPath.Substring (0, i);
				} else {
					locationSubPath = MachineWebPath;
					locationConfigPath = null;
				}
			}
			
			if (GetStreamName (configPath) == null) {
				// There is no config file for this path. Get the next one in the chain.
				InitForConfiguration (ref locationSubPath, out configPath, out locationConfigPath, root, hostInitConfigurationParams);
			}
		}
		
		public string MapPath (string virtualPath)
		{
			if (map != null)
				return MapPathFromMapper (virtualPath);
			else if (HttpContext.Current != null
				 && HttpContext.Current.Request != null)
				return HttpContext.Current.Request.MapPath (virtualPath);
			else
				return virtualPath;
		}
		
		public string NormalizeVirtualPath (string virtualPath)
		{
			if (virtualPath == null || virtualPath.Length == 0)
				virtualPath = ".";
			else
				virtualPath = virtualPath.Trim ();

			if (virtualPath [0] == '~' && virtualPath.Length > 2 && virtualPath [1] == '/')
				virtualPath = virtualPath.Substring (1);
				
			if (System.IO.Path.DirectorySeparatorChar != '/')
				virtualPath = virtualPath.Replace (System.IO.Path.DirectorySeparatorChar, '/');

			if (UrlUtils.IsRooted (virtualPath)) {
				virtualPath = UrlUtils.Canonic (virtualPath);
			} else {
				if (map.VirtualDirectories.Count > 0) {
					string root = map.VirtualDirectories [0].VirtualDirectory;
					virtualPath = UrlUtils.Combine (root, virtualPath);
					virtualPath = UrlUtils.Canonic (virtualPath);
				}
			}
			return virtualPath;
		}

		public string MapPathFromMapper (string virtualPath)
		{
			string path = NormalizeVirtualPath (virtualPath);
			
			foreach (VirtualDirectoryMapping mapping in map.VirtualDirectories) {
				if (path.StartsWith (mapping.VirtualDirectory)) {
					int i = mapping.VirtualDirectory.Length;
					if (path.Length == i) {
						return mapping.PhysicalDirectory;
					}
					else if (path [i] == '/') {
						string pathPart = path.Substring (i + 1).Replace ('/', Path.DirectorySeparatorChar);
						return Path.Combine (mapping.PhysicalDirectory, pathPart);
					}
				}
			}
			throw new HttpException ("Invalid virtual directory: " + virtualPath);
		}

		string GetWebConfigFileName (string dir)
		{
			string[] filenames = new string[] {"Web.Config", "Web.config", "web.config" };

			foreach (string fn in filenames) {
				string file = Path.Combine (dir, fn);
				if (File.Exists (file))
					return file;
			}

			return null;
		}
		
		public virtual bool IsAboveApplication (string configPath)
		{
			throw new NotImplementedException ();
		}
		
		public virtual bool IsConfigRecordRequired (string configPath)
		{
			throw new NotImplementedException ();
		}
		
		public virtual bool IsDefinitionAllowed (string configPath, ConfigurationAllowDefinition allowDefinition, ConfigurationAllowExeDefinition allowExeDefinition)
		{
			switch (allowDefinition) {
				case ConfigurationAllowDefinition.MachineOnly:
					return configPath == MachinePath || configPath == MachineWebPath;
				case ConfigurationAllowDefinition.MachineToWebRoot:
				case ConfigurationAllowDefinition.MachineToApplication:
					return configPath == MachinePath || configPath == MachineWebPath || configPath == "/";
				default:
					return true;
			}
		}
		
		public virtual bool IsFile (string streamName)
		{
			throw new NotImplementedException ();
		}
		
		public virtual bool IsLocationApplicable (string configPath)
		{
			throw new NotImplementedException ();
		}
		
		public virtual Stream OpenStreamForRead (string streamName)
		{
			if (!File.Exists (streamName))
				throw new ConfigurationException ("File '" + streamName + "' not found");
				
			return new FileStream (streamName, FileMode.Open, FileAccess.Read);
		}

		[MonoTODO]
		public virtual Stream OpenStreamForRead (string streamName, bool assertPermissions)
		{
			throw new NotImplementedException ();
		}

		public virtual Stream OpenStreamForWrite (string streamName, string templateStreamName, ref object writeContext)
		{
			return new FileStream (streamName, FileMode.Create, FileAccess.Write);
		}

		[MonoTODO]
		public virtual Stream OpenStreamForWrite (string streamName, string templateStreamName, ref object writeContext, bool assertPermissions)
		{
			throw new NotImplementedException ();
		}
		
		public virtual bool PrefetchAll (string configPath, string streamName)
		{
			throw new NotImplementedException ();
		}
		
		public virtual bool PrefetchSection (string sectionGroupName, string sectionName)
		{
			throw new NotImplementedException ();
		}

		[MonoTODO]
		public virtual void RequireCompleteInit (IInternalConfigRecord configRecord)
		{
			throw new NotImplementedException ();
		}

		public virtual object StartMonitoringStreamForChanges (string streamName, StreamChangeCallback callback)
		{
			throw new NotImplementedException ();
		}
		
		public virtual void StopMonitoringStreamForChanges (string streamName, StreamChangeCallback callback)
		{
			throw new NotImplementedException ();
		}
		
		public virtual void VerifyDefinitionAllowed (string configPath, ConfigurationAllowDefinition allowDefinition, ConfigurationAllowExeDefinition allowExeDefinition, IConfigErrorInfo errorInfo)
		{
			if (!IsDefinitionAllowed (configPath, allowDefinition, allowExeDefinition))
				throw new ConfigurationErrorsException ("The section can't be defined in this file (the allowed definition context is '" + allowDefinition + "').", errorInfo.Filename, errorInfo.LineNumber);
		}
		
		public virtual void WriteCompleted (string streamName, bool success, object writeContext)
		{
		}
		
		[MonoTODO]
		public virtual void WriteCompleted (string streamName, bool success, object writeContext, bool assertPermissions)
		{
		}

		public virtual bool SupportsChangeNotifications {
			get { return false; }
		}
		
		public virtual bool SupportsLocation {
			get { return false; }
		}
		
		public virtual bool SupportsPath {
			get { return false; }
		}
		
		public virtual bool SupportsRefresh {
			get { return false; }
		}

		[MonoTODO]
		public virtual bool IsRemote {
			get { return false; }
		}

		[MonoTODO]
		public virtual bool IsFullTrustSectionWithoutAptcaAllowed (IInternalConfigRecord configRecord)
		{
			throw new NotImplementedException ();
		}

		[MonoTODO]
		public virtual bool IsInitDelayed (IInternalConfigRecord configRecord)
		{
			throw new NotImplementedException ();
		}

		[MonoTODO]
		public virtual bool IsSecondaryRoot (string configPath)
		{
			throw new NotImplementedException ();
		}

		[MonoTODO]
		public virtual bool IsTrustedConfigPath (string configPath)
		{
			throw new NotImplementedException ();
		}
	}
}

#endif
