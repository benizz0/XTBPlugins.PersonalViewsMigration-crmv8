﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.UI.WebControls.WebParts;
using System.Windows.Forms;
using Carfup.XTBPlugins.AppCode;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using XrmToolBox.Extensibility;

namespace Carfup.XTBPlugins.Forms
{
    public partial class Sharings : Form
    {
        private PersonalViewsMigration.PersonalViewsMigration pvm;
        public List<Entity> sharingList = null;
        public string title = "Sharings";
        public bool isUserModified = false;
        public Dictionary<int, string> poaAccessMasksRights = new Dictionary<int, string>()
        {
            { 524288, "assign"},
            { 262144, "share"},
            { 65536, "delete"},
            { 4, "append"},
            { 2, "write"},
            { 1, "read"}
        };

        private List<SharingsWithDetailedAccessMask> sharingDetailsList = null;

        private BindingSource dgvSharingsSource = null;

        public Sharings(PersonalViewsMigration.PersonalViewsMigration pvm)
        {
            InitializeComponent();
            Application.EnableVisualStyles();
            this.pvm = pvm;
            this.pvm.log.LogData(EventType.Event, LogAction.ShowHelpScreen);


            sharingDetailsList = new List<SharingsWithDetailedAccessMask>();;
            dgvSharingsSource = new BindingSource()
            {
                DataSource = sharingDetailsList
            };
            dgvSharings.DataSource = dgvSharingsSource;
        }

        public void loadSharings(List<Entity> sharings, string filter = null)
        {
            if (sharings.Count == 0)
                return;

            sharingList = sharings;
            var listToKeep = sharings;
            
            sharingDetailsList.Clear();

            if (!string.IsNullOrEmpty(filter))
                listToKeep = sharingList.Where(x => (x.Contains("systemuser.domainname") && x.GetAttributeValue<AliasedValue>("systemuser.domainname").Value.ToString().ToLower().Contains(filter))
                                                    || (x.Contains("team.name") && x.GetAttributeValue<AliasedValue>("team.name").Value.ToString().ToLower().Contains(filter))).ToList();
            
            try
            {
                foreach (var sharing in listToKeep.OrderBy(x => x.GetAttributeValue<string>("principaltypecode")))
                {
                    var sharingDetails = grabSharingDetails(sharing);

                    sharingDetailsList.Add(sharingDetails);
                    //dgvSharingsSource.Add(sharingDetails);
                }
                dgvSharingsSource.ResetBindings(false);
            }
            catch (Exception e)
            {
                this.pvm.log.LogData(EventType.Exception, LogAction.SharingsToListViewLoaded, e);
                throw;
            }

            this.pvm.log.LogData(EventType.Event, LogAction.SharingsToListViewLoaded);

            for (int i = 0; i < dgvSharings.Columns.Count - 1; i++)
            {
                dgvSharings.Columns[i].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            }
        }

        private void btnDeleteSharings_Click(object sender, EventArgs e)
        {
            int revoked = 0;

            try
            {
                for (int i = 0; i < sharingDetailsList.Where(x => x.selected).Count(); i++)
                {
                    var sharing = sharingDetailsList[i];
                    // var sharingDetails = sharingList.FirstOrDefault(x => x.GetAttributeValue<Guid>("principalid") == (Guid)sharing.Tag);

                    var revokeAccessRequest = new RevokeAccessRequest
                    {
                        Revokee = new EntityReference(sharing.entity.GetAttributeValue<string>("principaltypecode"), sharing.entity.GetAttributeValue<Guid>("principalid")),
                        Target = new EntityReference(sharing.entity.GetAttributeValue<string>("objecttypecode"), sharing.entity.GetAttributeValue<Guid>("objectid")),
                    };

                    this.pvm.controllerManager.serviceClient.Execute(revokeAccessRequest);
                    //    listViewSharings.Items.Remove(sharing);

                    sharingDetailsList.Remove(sharingDetailsList.FirstOrDefault(x => x.entity.Id == sharing.entity.Id));

                    revoked++;
                }

                dgvSharingsSource.ResetBindings(false);
                //dgvSharingsSource.DataSource = sharingDetailsList;
                //dgvSharings.DataSource = dgvSharingsSource;
            }
            catch (Exception exception)
            {
                this.pvm.log.LogData(EventType.Exception, LogAction.SharingsRevoked, exception);
                throw;
            }
            

            this.pvm.log.LogData(EventType.Event, LogAction.SharingsRevoked);

            MessageBox.Show( $"You successfully revoked {revoked} sharings.{Environment.NewLine}You may close the this window now.", "Sharings revoked !", MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void linkLabelUnSelect_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            foreach (DataGridViewRow item in dgvSharings.Rows)
                item.Cells[0].Value = false;
        }

        private void linkLabelSelectAll_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            foreach (DataGridViewRow item in dgvSharings.Rows)
                item.Cells[0].Value = true;
        }

        private void Sharings_Load(object sender, EventArgs e)
        {
            this.Text = title;
        }

        private void btnDeleteSharings_Leave(object sender, EventArgs e)
        {
            if(isUserModified)
                this.pvm.controllerManager.userManager.ManageImpersonification(isUserModified);
        }

        private void textBoxFilterSharings_TextChanged(object sender, EventArgs e)
        {
            var filter = textBoxFilterSharings.Text;

            if (filter.Length > 1)
                loadSharings(sharingList, filter.ToLower());
            else if (filter == "")
                loadSharings(sharingList);
        }

        private void textBoxFilterSharings_Click(object sender, EventArgs e)
        {
            if (textBoxFilterSharings.Text == "Search in results ...")
                textBoxFilterSharings.Text = "";
        }

        private SharingsWithDetailedAccessMask grabSharingDetails(Entity sharing)
        {
            var result = new SharingsWithDetailedAccessMask(sharing);
            int arm = sharing.GetAttributeValue<int>("accessrightsmask");
            int valueleft = arm;

            string userteam = "";//sharing.GetAttributeValue<string>("name");
            if (sharing.GetAttributeValue<string>("principaltypecode") == "systemuser")
                userteam = sharing.GetAttributeValue<AliasedValue>("systemuser.domainname").Value.ToString();
            if (sharing.GetAttributeValue<string>("principaltypecode") == "team")
            {
                userteam = sharing.GetAttributeValue<AliasedValue>("team.name").Value.ToString();
                //lvGroup = listViewSharings.Groups[1];
            }



            result.sharedWith = userteam;

            foreach (var right in poaAccessMasksRights)
            {
                var enabled = valueleft >= right.Key;
                switch (right.Value)
                {
                    case "read":
                        result.read = enabled;
                        break;
                    case "write":
                        result.write = enabled;
                        break;
                  
                    case "append":
                        result.append = enabled;
                        break;

                    case "assign":
                        result.assign = enabled;
                        break;

                    case "delete":
                        result.delete = enabled;
                        break;

                    case "share":
                        result.share = enabled;
                        break;
                }

                if (enabled)
                    valueleft -= right.Key;
            }

            return result;
        }

        private void dgvSharings_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex == -1)
                return;

            var row = dgvSharings.Rows[e.RowIndex];
            ((SharingsWithDetailedAccessMask)row.DataBoundItem).modified = true;
        }

        private void btnModifySharings_Click(object sender, EventArgs e)
        {
            var request = new ExecuteMultipleRequest()
            {
                Requests = new OrganizationRequestCollection(),
                Settings = new ExecuteMultipleSettings
                {
                    ContinueOnError = false,
                    ReturnResponses = true
                }
            };

            foreach (var item in sharingDetailsList.Where(x => x.modified))
            {
             
                var modifyAccess = new ModifyAccessRequest()
                {
                    Target = new EntityReference(item.entity.GetAttributeValue<string>("objecttypecode"), item.entity.GetAttributeValue<Guid>("objectid")),
                    PrincipalAccess = new PrincipalAccess()
                    {
                        AccessMask = getNewAccessRightMask(item),
                        Principal = new EntityReference(item.entity.GetAttributeValue<string>("principaltypecode"), item.entity.GetAttributeValue<Guid>("principalid"))
                    }
                };
                
                this.pvm.controllerManager.serviceClient.Execute(modifyAccess);
            }

            MessageBox.Show("Update successfull");
        }

        private AccessRights getNewAccessRightMask(SharingsWithDetailedAccessMask sharing)
        {
            List<AccessRights> masks = new List<AccessRights>();

            if (sharing.read)
                masks.Add(AccessRights.ReadAccess);
            if (sharing.write)
                masks.Add(AccessRights.WriteAccess);
            if (sharing.append)
                masks.Add(AccessRights.AppendAccess);
            if (sharing.delete)
                masks.Add(AccessRights.DeleteAccess);
            if (sharing.share)
                masks.Add(AccessRights.ShareAccess);
            if (sharing.assign)
                masks.Add(AccessRights.AssignAccess);


            AccessRights mask = masks[0];
            for(int i = 1; i < masks.Count; i++)
                mask |= masks[i];

            return mask;
        }
    }


    public class SharingsWithDetailedAccessMask
    {
        [DisplayName("Selected")]
        public bool selected { get; set; }

        [DisplayName("Shared With")]
        public string sharedWith { get; set; }

        [DisplayName("Access")]
        public int accessMask
        {
            get { return entity.GetAttributeValue<int>("accessrightsmask"); }
        }

        [DisplayName("Shared On")]
        public string sharedOn {
            get { return entity.GetAttributeValue<DateTime>("changedon").ToLocalTime().ToString("dd-MMM-yyyy HH:mm"); }
        }

        [DisplayName("Read")]
        public bool read { get; set; }

        [DisplayName("Write")]
        public bool write { get; set; }

        [DisplayName("Delete")]
        public bool delete { get; set; }

        [DisplayName("Append")]
        public bool append { get; set; }


        [DisplayName("Assign")]
        public bool assign { get; set; }


        [DisplayName("Share")]
        public bool share { get; set; }

        [Browsable(false)]
        public Entity entity { get;  }

        [Browsable(false)]
        public Guid sharingId { get {return entity.Id; }  }
        [Browsable(false)]
        public bool modified { get; set; }

        public SharingsWithDetailedAccessMask(Entity sharing)
        {
            entity = sharing;
        }
    }
}
