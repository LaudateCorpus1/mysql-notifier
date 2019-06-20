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

using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using MySql.Notifier.Classes;
using MySql.Notifier.Enumerations;
using MySql.Notifier.Properties;
using MySql.Utility.Classes;
using MySql.Utility.Classes.MySqlWorkbench;
using MySql.Utility.Forms;

namespace MySql.Notifier.Forms
{
  /// <summary>
  /// Dialog window where users manage the monitored local and remote services and MySQL instances.
  /// </summary>
  public partial class ManageItemsDialog : MachineAwareForm
  {
    #region Fields

    /// <summary>
    /// The service or instance object selected by the user from the corresponding list view.
    /// </summary>
    private object _selectedItem;

    /// <summary>
    /// Flag indicating if any of the maintained instances properties changed and need to be saved.
    /// </summary>
    private bool _instancesHaveChanges;

    /// <summary>
    /// Flag indicating if any of the maintained services properties changed and need to be saved.
    /// </summary>
    private bool _servicesHaveChanges;

    #endregion Fields

    /// <summary>
    /// Initializes a new instance of the <see cref="ManageItemsDialog"/> class.
    /// </summary>
    /// <param name="instancesList">List of <see cref="MySqlInstance"/> objects.</param>
    /// <param name="machinesList">List of <see cref="Machine"/> objects.</param>
    public ManageItemsDialog(MySqlInstancesList instancesList, MachinesList machinesList)
    {
      _selectedItem = null;
      _instancesHaveChanges = false;
      _servicesHaveChanges = false;
      InitializeComponent();
      InstancesList = instancesList;
      InstancesListChanged = false;
      MachinesList = machinesList;
      RefreshServicesAndInstancesListViews();
      SetDialogControlsAvailability();
    }

    #region Enums

    /// <summary>
    /// Specifies the monitored item type.
    /// </summary>
    public enum MonitoredItemType
    {
      /// <summary>
      /// Local or remote Windows service.
      /// </summary>
      Service = 0,

      /// <summary>
      /// MySQL server instance.
      /// </summary>
      MySqlInstance = 1,

      /// <summary>
      /// Default value.
      /// </summary>
      None = -1
    }

    #endregion Enums

    #region Properties

    /// <summary>
    /// Gets an object representing a list of <see cref="MySqlInstance"/> objects used to monitor MySQL Server instances.
    /// </summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public MySqlInstancesList InstancesList { get; }

    /// <summary>
    /// Gets a value indicating whether the instances list changed.
    /// </summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool InstancesListChanged { get; private set; }

    #endregion Properties

    /// <summary>
    /// Event delegate method fired when the <see cref="AddButton"/> button is clicked.
    /// </summary>
    /// <param name="sender">Sender object.</param>
    /// <param name="e">Event arguments.</param>
    private void AddButton_Click(object sender, EventArgs e)
    {
      var screenPoint = AddButton.PointToScreen(new Point(AddButton.Left, AddButton.Bottom));
      AddButtonContextMenuStrip.Show(AddButton,
        screenPoint.Y + AddButtonContextMenuStrip.Size.Height > Screen.PrimaryScreen.WorkingArea.Height
          ? new Point(0, -AddButtonContextMenuStrip.Size.Height)
          : new Point(0, AddButton.Height));
    }

    /// <summary>
    /// Adds an instance to the list of instances.
    /// </summary>
    /// <param name="instance">Instance to add.</param>
    /// <param name="setPage">Flag indicating if the Instances tab must be focused.</param>
    private void AddInstance(MySqlInstance instance, bool setPage)
    {
      var newItem = new ListViewItem(instance.DisplayConnectionSummaryText)
      {
        Tag = instance
      };
      newItem.SubItems.Add(instance.WorkbenchConnection.ConnectionMethod.GetDescription());
      newItem.SubItems.Add(instance.ConnectionStatusText);
      MonitoredInstancesListView.Items.Add(newItem);
      if (!setPage)
      {
        return;
      }

      ItemsTabControl.SelectedIndex = 1;
      newItem.Selected = true;
    }

    /// <summary>
    /// Adds a service to the list of services.
    /// </summary>
    /// <param name="service">Service to add.</param>
    /// <param name="machine">Machine containing the service.</param>
    /// <param name="setPage">Flag indicating if the Services tab must be focused.</param>
    private void AddService(MySqlService service, Machine machine, bool setPage)
    {
      if (service == null)
      {
        return;
      }

      if (service.Host == null)
      {
        service.Host = machine;
        service.SetServiceParameters(true);
      }

      var newItem = new ListViewItem(service.DisplayName) { Tag = service };
      newItem.SubItems.Add(machine.Name);
      newItem.SubItems.Add(service.Status.ToString());
      MonitoredServicesListView.Items.Add(newItem);

      if (!setPage)
      {
        return;
      }

      ItemsTabControl.SelectedIndex = 0;
      newItem.Selected = true;
    }

    /// <summary>
    /// Event delegate method fired when the <see cref="DeleteButton"/> button is clicked.
    /// </summary>
    /// <param name="sender">Sender object.</param>
    /// <param name="e">Event arguments.</param>
    private void DeleteButton_Click(object sender, EventArgs e)
    {
      switch (_selectedItem)
      {
        case null:
          return;

        case MySqlService selectedService:
        {
          var machine = MachinesList.GetMachineById(selectedService.Host.MachineId);
          machine.ChangeService(selectedService, ListChangeType.RemoveByUser);
          MonitoredServicesListView.Items.RemoveAt(MonitoredServicesListView.SelectedIndices[0]);
          break;
        }

        case MySqlInstance selectedInstance:
        {
          if (InstancesList.Remove(selectedInstance))
          {
            MonitoredInstancesListView.Items.RemoveAt(MonitoredInstancesListView.SelectedIndices[0]);
          }

          break;
        }
      }
    }

    /// <summary>
    /// Event delegate method fired when the <see cref="ManageItemsDialog"/> button is closed.
    /// </summary>
    /// <param name="sender">Sender object.</param>
    /// <param name="e">Event arguments.</param>
    private void ManageItemsDialog_FormClosed(object sender, FormClosedEventArgs e)
    {
      if (DialogResult != DialogResult.OK)
      {
        return;
      }

      if (_instancesHaveChanges)
      {
        InstancesList.SaveToFile();
      }

      if (_servicesHaveChanges)
      {
        MachinesList.SaveToFile();
      }
    }

    /// <summary>
    /// Event delegate method fired when the <see cref="InstanceMonitorIntervalNumericUpDown"/> value changes.
    /// </summary>
    /// <param name="sender">Sender object.</param>
    /// <param name="e">Event arguments.</param>
    private void InstanceMonitorIntervalNumericUpDown_ValueChanged(object sender, EventArgs e)
    {
      if (!(_selectedItem is MySqlInstance selectedInstance))
      {
        return;
      }

      selectedInstance.MonitoringInterval = (uint)InstanceMonitorIntervalNumericUpDown.Value;
      _instancesHaveChanges = true;
    }

    /// <summary>
    /// Event delegate method fired when the <see cref="InstanceMonitorIntervalUOMComboBox"/> selected index changes.
    /// </summary>
    /// <param name="sender">Sender object.</param>
    /// <param name="e">Event arguments.</param>
    private void InstanceMonitorIntervalUOMComboBox_SelectedIndexChanged(object sender, EventArgs e)
    {
      if (!(_selectedItem is MySqlInstance selectedInstance))
      {
        return;
      }

      selectedInstance.MonitoringIntervalUnitOfMeasure = (TimeUtilities.IntervalUnitOfMeasure)InstanceMonitorIntervalUOMComboBox.SelectedIndex;
      _instancesHaveChanges = true;
    }

    /// <summary>
    /// Event delegate method fired when the <see cref="ItemsTabControl"/> selected index changes.
    /// </summary>
    /// <param name="sender">Sender object.</param>
    /// <param name="e">Event arguments.</param>
    private void ItemsTabControl_SelectedIndexChanged(object sender, EventArgs e)
    {
      switch (ItemsTabControl.SelectedIndex)
      {
        case 0:
          // Services tab page
          MonitoredServicesListView_SelectedIndexChanged(MonitoredServicesListView, EventArgs.Empty);
          break;

        case 1:
          // Instances tab page
          MonitoredInstancesListView_SelectedIndexChanged(MonitoredInstancesListView, EventArgs.Empty);
          break;

        default:
          _selectedItem = null;
          SetDialogControlsAvailability();
          break;
      }
    }

    /// <summary>
    /// Event delegate method fired when the <see cref="MonitoredInstancesListView"/> selected index changes.
    /// </summary>
    /// <param name="sender">Sender object.</param>
    /// <param name="e">Event arguments.</param>
    private void MonitoredInstancesListView_SelectedIndexChanged(object sender, EventArgs e)
    {
      _selectedItem = MonitoredInstancesListView.SelectedItems.Count > 0
        ? MonitoredInstancesListView.SelectedItems[0].Tag
        : null;
      SetDialogControlsAvailability();
    }

    /// <summary>
    /// Event delegate method fired when the <see cref="MonitoredServicesListView"/> selected index changes.
    /// </summary>
    /// <param name="sender">Sender object.</param>
    /// <param name="e">Event arguments.</param>
    private void MonitoredServicesListView_SelectedIndexChanged(object sender, EventArgs e)
    {
      _selectedItem = MonitoredServicesListView.SelectedItems.Count > 0
        ? MonitoredServicesListView.SelectedItems[0].Tag
        : null;
      SetDialogControlsAvailability();
    }

    /// <summary>
    /// Event delegate method fired when the <see cref="MySQLInstanceToolStripMenuItem"/> context menu item is clicked.
    /// </summary>
    /// <param name="sender">Sender object.</param>
    /// <param name="e">Event arguments.</param>
    private void MySQLInstanceToolStripMenuItem_Click(object sender, EventArgs e)
    {
      Cursor.Current = Cursors.WaitCursor;
      MonitoredServicesListView.BeginUpdate();
      MySqlWorkbenchConnection selectedConnection = null;

      using (var monitorInstancesDialog = new MonitorMySqlServerInstancesDialog(MachinesList, InstancesList))
      {
        if (monitorInstancesDialog.ShowDialog() == DialogResult.OK)
        {
          selectedConnection = monitorInstancesDialog.SelectedWorkbenchConnection;
          var mySqlInstanceAndExistingFlag = InstancesList.AddConnectionToMonitor(selectedConnection);
          if (mySqlInstanceAndExistingFlag != null)
          {
            var instance = mySqlInstanceAndExistingFlag.Item1;
            var instanceAlreadyExists = mySqlInstanceAndExistingFlag.Item2;
            if (instanceAlreadyExists)
            {
              var correspondingListViewItem = MonitoredInstancesListView.Items.OfType<ListViewItem>().FirstOrDefault(lvi => lvi.Tag is MySqlInstance existingInstance
                                                                                                                            && existingInstance == instance);
              if (correspondingListViewItem != null)
              {
                correspondingListViewItem.Text = instance.DisplayConnectionSummaryText;
                correspondingListViewItem.SubItems[1].Text = instance.WorkbenchConnection.ConnectionMethod.GetDescription();
                correspondingListViewItem.SubItems[2].Text = instance.ConnectionStatusText;
              }
            }
            else
            {
              AddInstance(instance, true);
            }
          }
        }

        // Workbench connections may have been edited so we may need to refresh the items in the list.
        foreach (ListViewItem lvi in MonitoredInstancesListView.Items)
        {
          if (!(lvi.Tag is MySqlInstance existingInstance)
              || selectedConnection != null
                 && existingInstance.WorkbenchConnection.Id == selectedConnection.Id)
          {
            continue;
          }

          var connectionInDisk = MySqlWorkbench.Connections.GetConnectionForId(existingInstance.WorkbenchConnection.Id);
          if (connectionInDisk == null
              || connectionInDisk.Equals(existingInstance.WorkbenchConnection))
          {
            continue;
          }

          lvi.Text = connectionInDisk.HostIdentifier;
          lvi.SubItems[1].Text = connectionInDisk.ConnectionMethod.GetDescription();
          lvi.SubItems[2].Text = connectionInDisk.ConnectionStatusText;
        }

        InstancesListChanged = monitorInstancesDialog.InstancesListChanged;
      }

      MonitoredServicesListView.EndUpdate();
      Cursor.Current = Cursors.Default;
    }

    /// <summary>
    /// Event delegate method fired when the <see cref="NotifyOnStatusChangeCheckBox"/> checked status changes.
    /// </summary>
    /// <param name="sender">Sender object.</param>
    /// <param name="e">Event arguments.</param>
    private void NotifyOnStatusChangeCheckBox_CheckedChanged(object sender, EventArgs e)
    {
      switch (_selectedItem)
      {
        case null:
          return;

        case MySqlService selectedService:
        {
          selectedService.NotifyOnStatusChange = NotifyOnStatusChangeCheckBox.Checked;
          _servicesHaveChanges = true;
          break;
        }

        case MySqlInstance selectedInstance:
        {
          selectedInstance.MonitorAndNotifyStatus = NotifyOnStatusChangeCheckBox.Checked;
          _instancesHaveChanges = true;
          break;
        }
      }
    }

    /// <summary>
    /// Refreshes the contents of the services and instances list view controls.
    /// </summary>
    private void RefreshServicesAndInstancesListViews()
    {
      // Set cursor to waiting, stop painting of list views to avoid flickering and clear their items.
      Cursor.Current = Cursors.WaitCursor;
      MonitoredServicesListView.BeginUpdate();
      MonitoredInstancesListView.BeginUpdate();
      MonitoredServicesListView.Items.Clear();
      MonitoredInstancesListView.Items.Clear();

      // Add monitored services.
      foreach (var machine in MachinesList.Machines)
      {
        foreach (var service in machine.Services)
        {
          AddService(service, machine, false);
        }
      }

      // Add monitored instances.
      foreach (var instance in InstancesList)
      {
        AddInstance(instance, false);
      }

      // Select automatically the first itemText or disable controls if no items exist.
      var pageListView = ItemsTabControl.SelectedIndex == 0 ? MonitoredServicesListView : MonitoredInstancesListView;
      if (pageListView.Items.Count > 0)
      {
        pageListView.Items[0].Selected = true;
      }
      else
      {
        pageListView.SelectedItems.Clear();
      }

      MonitoredServicesListView.EndUpdate();
      MonitoredInstancesListView.EndUpdate();

      // Revert cursor back to normal and paint changes in list.
      Cursor.Current = Cursors.Default;
    }

    /// <summary>
    /// Event delegate method fired when the <see cref="ServiceToolStripMenuItem"/> context menu item is clicked.
    /// </summary>
    /// <param name="sender">Sender object.</param>
    /// <param name="e">Event arguments.</param>
    private void ServiceToolStripMenuItem_Click(object sender, EventArgs e)
    {
      using (var dialog = new AddServiceDialog(MachinesList))
      {
        if (dialog.ShowDialog() == DialogResult.OK)
        {
          if (dialog.NewMachine != null && dialog.ServicesToAdd != null && dialog.ServicesToAdd.Count > 0)
          {
            NewMachine = MachinesList.GetMachineById(dialog.NewMachine.MachineId);
            if (NewMachine == null)
            {
              MachinesList.ChangeMachine(dialog.NewMachine, ListChangeType.AddByUser);
              NewMachine = dialog.NewMachine;
            }

            foreach (var service in dialog.ServicesToAdd)
            {
              if (NewMachine.ContainsService(service))
              {
                InfoDialog.ShowDialog(InfoDialogProperties.GetWarningDialogProperties(Resources.WarningText, Resources.ServiceAlreadyInListWarningText));
              }
              else
              {
                NewMachine.ChangeService(service, ListChangeType.AddByUser);
                AddService(service, NewMachine, true);
              }
            }
          }
        }

        if (dialog.HasChanges)
        {
          RefreshServicesAndInstancesListViews();
        }
      }
    }

    /// <summary>
    /// Sets the availability of controls related to services or instances based on the selected item.
    /// </summary>
    private void SetDialogControlsAvailability()
    {
      DeleteButton.Enabled = _selectedItem != null;
      NotifyOnStatusChangeCheckBox.Enabled = DeleteButton.Enabled;
      UpdateTrayIconCheckBox.Enabled = DeleteButton.Enabled;

      switch (_selectedItem)
      {
        case null:
          NotifyOnStatusChangeCheckBox.Checked = false;
          UpdateTrayIconCheckBox.Checked = false;
          InstanceMonitorIntervalNumericUpDown.Value = 0;
          InstanceMonitorIntervalNumericUpDown.Enabled = false;
          InstanceMonitorIntervalUOMComboBox.Text = string.Empty;
          InstanceMonitorIntervalUOMComboBox.Enabled = false;
          break;

        case MySqlService service:
        {
          NotifyOnStatusChangeCheckBox.Checked = service.NotifyOnStatusChange;
          UpdateTrayIconCheckBox.Checked = service.UpdateTrayIconOnStatusChange;
          InstanceMonitorIntervalNumericUpDown.Value = 0;
          InstanceMonitorIntervalNumericUpDown.Enabled = false;
          InstanceMonitorIntervalUOMComboBox.Text = string.Empty;
          InstanceMonitorIntervalUOMComboBox.Enabled = false;
          break;
        }
        case MySqlInstance instance:
        {
          NotifyOnStatusChangeCheckBox.Checked = instance.MonitorAndNotifyStatus;
          UpdateTrayIconCheckBox.Checked = instance.UpdateTrayIconOnStatusChange;
          InstanceMonitorIntervalNumericUpDown.Enabled = true;
          InstanceMonitorIntervalNumericUpDown.Value = instance.MonitoringInterval;
          InstanceMonitorIntervalUOMComboBox.Enabled = true;
          InstanceMonitorIntervalUOMComboBox.SelectedIndex = (int)instance.MonitoringIntervalUnitOfMeasure;
          break;
        }
      }
    }

    /// <summary>
    /// Event delegate method fired when the <see cref="UpdateTrayIconCheckBox"/> checked status changes.
    /// </summary>
    /// <param name="sender">Sender object.</param>
    /// <param name="e">Event arguments.</param>
    private void UpdateTrayIconCheckBox_CheckedChanged(object sender, EventArgs e)
    {
      switch (_selectedItem)
      {
        case null:
          return;

        case MySqlService selectedService:
        {
          selectedService.UpdateTrayIconOnStatusChange = UpdateTrayIconCheckBox.Checked;
          _servicesHaveChanges = true;
          break;
        }

        case MySqlInstance selectedInstance:
        {
          selectedInstance.UpdateTrayIconOnStatusChange = UpdateTrayIconCheckBox.Checked;
          _instancesHaveChanges = true;
          break;
        }
      }
    }
  }
}