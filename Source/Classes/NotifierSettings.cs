﻿// Copyright (c) 2012, 2019, Oracle and/or its affiliates. All rights reserved.
//
// This program is free software; you can redistribute it and/or
// modify it under the terms of the GNU General Public License as
// published by the Free Software Foundation; version 2 of the
// License.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program; if not, write to the Free Software
// Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA
// 02110-1301  USA

using MySql.Utility.Classes;

namespace MySql.Notifier.Classes
{
  /// <summary>
  /// A settings provider customized for MySQL Notifier.
  /// </summary>
  public class NotifierSettings : CustomSettingsProvider
  {
    /// <summary>
    /// The text from <see cref="AssemblyInfo.AssemblyTitle"/> stripped of spaces.
    /// </summary>
    private string _assemblyTitleWithoutSpaces;

    /// <summary>
    /// Gets or sets the name used for the root XML element of the settings file.
    /// </summary>
    public static string RootElementName { get; set; }

    /// <summary>
    /// Gets or sets the name used for the root XML element of the settings file.
    /// </summary>
    public override string RootElementApplicationName
    {
      get
      {
        if (string.IsNullOrEmpty(_assemblyTitleWithoutSpaces))
        {
          _assemblyTitleWithoutSpaces = string.IsNullOrEmpty(AssemblyInfo.AssemblyTitle)
            ? "settings"
            : AssemblyInfo.AssemblyTitle.Replace(" ", string.Empty);
        }

        return string.IsNullOrEmpty(RootElementName)
          ? _assemblyTitleWithoutSpaces
          : RootElementName;
      }
    }

    /// <summary>
    /// Gets the fle path for the settings file.
    /// </summary>
    public static string SettingsFilePath
    {
      get
      {
        return Program.EnvironmentApplicationDataDirectory + Program.SETTINGS_FILE_RELATIVE_PATH;
      }
    }

    /// <summary>
    /// Gets the name of this application.
    /// </summary>
    public override string ApplicationName
    {
      get
      {
        return AssemblyInfo.AssemblyTitle;
      }

      set
      {
      }
    }

    /// <summary>
    /// Gets the custom path where the settings file is saved.
    /// </summary>
    public override string SettingsPath
    {
      get
      {
        return SettingsFilePath;
      }
    }
  }
}