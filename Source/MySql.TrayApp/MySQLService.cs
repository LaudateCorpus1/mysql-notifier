﻿// Copyright (c) 2006-2008 MySQL AB, 2008-2009 Sun Microsystems, Inc.
//
// MySQL Connector/NET is licensed under the terms of the GPLv2
// <http://www.gnu.org/licenses/old-licenses/gpl-2.0.html>, like most 
// MySQL Connectors. There are special exceptions to the terms and 
// conditions of the GPLv2 as it is applied to this software, see the 
// FLOSS License Exception
// <http://www.mysql.com/about/legal/licensing/foss-exception.html>.
//
// This program is free software; you can redistribute it and/or modify 
// it under the terms of the GNU General Public License as published 
// by the Free Software Foundation; version 2 of the License.
//
// This program is distributed in the hope that it will be useful, but 
// WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY 
// or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License 
// for more details.
//
// You should have received a copy of the GNU General Public License along 
// with this program; if not, write to the Free Software Foundation, Inc., 
// 51 Franklin St, Fifth Floor, Boston, MA 02110-1301  USA

using System;
using System.Collections.Generic;
using System.Collections;
using System.ServiceProcess;
using System.Windows.Forms;
using System.Linq;
using System.Globalization;
using System.Management;

namespace MySql.TrayApp
{

  class MySQLService : IDisposable
  {   
    private const int DEFAULT_TIMEOUT = 5000;
    private int timeoutMilliseconds = DEFAULT_TIMEOUT;
    private ServiceControllerStatus previousStatus;
    private bool disposed = false;
    private readonly bool hasAdminPrivileges = false;

    private ServiceController winService;
    private ServiceMenuGroup menuGroup;
   

    public bool HasAdminPrivileges
    {
      get { return hasAdminPrivileges; }
    }

    public static int DefaultTimeOut
    {
      get { return DEFAULT_TIMEOUT; }
    }

    public int TimeOutMilliseconds
    {
      set {
          timeoutMilliseconds = value; 
      }
      get { return timeoutMilliseconds; 
      }
    }

    public string ServiceName
    {
      get { return winService.ServiceName; }
    }

    public ServiceMenuGroup MenuGroup
    {
      get { return menuGroup; }
    }

    public ServiceControllerStatus RefreshStatus
    {
      get
      {
        winService.Refresh();
        var newStatus = winService.Status;
        var copyPrevStatus = previousStatus;
        if (!previousStatus.Equals(newStatus))
        {
          previousStatus = newStatus;
          OnStatusChanged(new ServiceStatus(winService.ServiceName, copyPrevStatus, newStatus));
        }
        return newStatus;
      }
    }

    public ServiceControllerStatus CurrentStatus
    {
      get { return winService.Status; }
    }

    public ServiceControllerStatus PreviousStatus
    {
      get { return previousStatus; }
    }


    public MySQLService(string serviceName, bool adminPrivileges)
    {
      hasAdminPrivileges = adminPrivileges;
      winService = new ServiceController(serviceName);
      try
      {
        previousStatus = winService.Status;
        menuGroup = new ServiceMenuGroup(this);
      }
      catch (InvalidOperationException ioEx)
      {
        MessageBox.Show(ioEx.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        winService = null;
      }
    }

    /// <summary>
    /// Cleans-up resources
    /// </summary>
    public void Dispose()
    {
      Dispose(true);
      GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes of managed and unmanaged resources.
    /// </summary>
    /// <param name="disposing">If true, the method has been called directly or indirectly by a user's code. Managed and unmanaged
    /// resources can be disposed. If false, the method has been called by the runtime from inside the finalizer and you should not
    /// reference other objects. Only unmanaged resources can be disposed.</param>
    protected virtual void Dispose(bool disposing)
    {
      if (!disposed)
      {
        if (disposing)
        {
          if (winService != null)
            winService.Dispose();
          if (menuGroup != null)
            menuGroup.Dispose();
        }
      }
      disposed = true;
    }

    public delegate void StatusChangedHandler(object sender, ServiceStatus args);

    /// <summary>
    /// Notifies that the status of the Windows Service has changed
    /// </summary>
    public event StatusChangedHandler StatusChanged;

    /// <summary>
    /// Notifies that this object is in the process of being disposed
    /// </summary>
    public event EventHandler Disposing;

    /// <summary>
    /// Invokes StatusChanged event when an action causes a status change
    /// </summary>
    /// <param name="e">Event arguments</param>
    protected virtual void OnStatusChanged(ServiceStatus args)
    {
      if (this.StatusChanged != null)
        this.StatusChanged(this, args);
    }

    /// <summary>
    /// Invokes Disposing event just before this object is disposed
    /// </summary>
    /// <param name="e">Event arguments</param>
    protected virtual void OnDisposing(EventArgs e)
    {
      if (this.Disposing != null)
        this.Disposing(this, e);
    }


    /// <summary>
    /// Attempts to start the current MySQL Service
    /// </summary>
    /// <returns>Flag indicating if the action completed succesfully</returns>
    public bool Start()
    {
      if (winService == null)
        return false;

      bool success = false;
      try
      {
        TimeSpan timeout = TimeSpan.FromMilliseconds(timeoutMilliseconds);
        winService.Start();
        winService.WaitForStatus(ServiceControllerStatus.Running, timeout);
        previousStatus = RefreshStatus;
        success = true;
      }
      catch (ArgumentException argEx)
      {
        MessageBox.Show(argEx.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
      }
      catch (System.ServiceProcess.TimeoutException toEx)
      {
        MessageBox.Show(toEx.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
      }
      catch (Exception ex)
      {
        MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
      }
      return success;
    }

    /// <summary>
    /// Attempts to stop the current MySQL Service
    /// </summary>
    /// <returns>Flag indicating if the action completed succesfully</returns>
    public bool Stop()
    {
      if (winService == null || !winService.CanStop)
        return false;

      bool success = false;
      try
      {
        TimeSpan timeout = TimeSpan.FromMilliseconds(timeoutMilliseconds);
        winService.Stop();
        winService.WaitForStatus(ServiceControllerStatus.Stopped, timeout);
        success = true;
        previousStatus = this.RefreshStatus;
      }
      catch (ArgumentException argEx)
      {
        MessageBox.Show(argEx.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
      }
      catch (System.ServiceProcess.TimeoutException toEx)
      {
        MessageBox.Show(toEx.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
      }
      catch (Exception ex)
      {
        MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
      }
      return success;
    }

    /// <summary>
    /// Attempts to stop and then start the current MySQL Service
    /// </summary>
    /// <returns>Flag indicating if the action completed succesfully</returns>
    public bool Restart()
    {
      if (winService == null)
        return false;

      bool success = false;
      try
      {
        int millisec1 = Environment.TickCount;
        TimeSpan timeout = TimeSpan.FromMilliseconds(timeoutMilliseconds);

        winService.Stop();
        winService.WaitForStatus(ServiceControllerStatus.Stopped, timeout);

        // count the rest of the timeout
        int millisec2 = Environment.TickCount;
        timeout = TimeSpan.FromMilliseconds(timeoutMilliseconds - (millisec2 - millisec1));

        winService.Start();
        winService.WaitForStatus(ServiceControllerStatus.Running, timeout);

        success = true;
        previousStatus = this.RefreshStatus;
      }
      catch (ArgumentException argEx)
      {
        MessageBox.Show(argEx.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
      }
      catch (System.ServiceProcess.TimeoutException toEx)
      {
        MessageBox.Show(toEx.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
      }
      catch (Exception ex)
      {
        MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
      }
      return success;
    }
  }

  /// <summary>
  /// Event Arguments used with the ServicesListChanged event containing information about the change.
  /// </summary>
  class ServicesListChangedArgs : EventArgs
  {
    
    private ArrayList addedServicesList;
    private ArrayList removedServicesList;


    public ArrayList AddedServicesList
    {
      get { return this.addedServicesList; }
    }

    public ArrayList RemovedServicesList
    {
      get { return this.removedServicesList; }
    }

    public int ChangedServicesCount
    {
      get { return this.addedServicesList.Count + this.removedServicesList.Count; }
    }


    public ServicesListChangedArgs(ArrayList addedServices, ArrayList removedServices)
    {
      this.addedServicesList = addedServices;
      this.removedServicesList = removedServices;
    }
  }

}