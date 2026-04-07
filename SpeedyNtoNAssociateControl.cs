using McTools.Xrm.Connection;
using Microsoft.Xrm.Sdk;
using SpeedyNtoNAssociatePlugin.Models;
using SpeedyNtoNAssociatePlugin.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using XrmToolBox.Extensibility;

namespace SpeedyNtoNAssociatePlugin
{
    public partial class SpeedyNtoNAssociateControl : PluginControlBase
    {
        private readonly MetadataService _metadataService = new MetadataService();
        private readonly DataSourceService _dataSourceService = new DataSourceService();
        private readonly AssociationEngine _engine = new AssociationEngine();

        private List<AssociationPair> _loadedPairs;
        private List<Tuple<string, string>> _allEntities;
        private List<RelationshipInfo> _allRelationships;
        private CancellationTokenSource _cts;
        private bool _isRunning;
        private bool _suppressEntityChanged;

        public SpeedyNtoNAssociateControl()
        {
            InitializeComponent();
        }

        private void SpeedyNtoNAssociateControl_Load(object sender, EventArgs e)
        {
            UpdateConnectionState();
        }

        public override void UpdateConnection(IOrganizationService newService,
            ConnectionDetail detail, string actionName, object parameter)
        {
            base.UpdateConnection(newService, detail, actionName, parameter);
            UpdateConnectionState();

            cmbEntity1.Items.Clear();
            cmbEntity2.Items.Clear();
            cmbRelationship.Items.Clear();
            cmbEntity1.Enabled = false;
            cmbEntity2.Enabled = false;
            cmbRelationship.Enabled = false;
            _loadedPairs = null;

            if (Service != null)
            {
                var recommended = _engine.GetRecommendedParallelism(Service);
                nudParallelism.Value = Math.Min(Math.Max(recommended, 1), (int)nudParallelism.Maximum);
            }
        }

        private void UpdateConnectionState()
        {
            bool connected = Service != null;
            btnLoadEntities.Enabled = connected;
            btnStart.Enabled = connected && !_isRunning;

            if (connected)
            {
                lblStatus.Text = $"Connected to: {ConnectionDetail?.OrganizationFriendlyName}";
                lblStatus.ForeColor = System.Drawing.Color.Green;
            }
            else
            {
                lblStatus.Text = "Not connected. Use the connection button above to connect.";
                lblStatus.ForeColor = System.Drawing.Color.Gray;
            }
        }

        #region Entity & Relationship Loading

        private void btnLoadEntities_Click(object sender, EventArgs e)
        {
            ExecuteMethod(LoadEntities);
        }

        private void LoadEntities()
        {
            WorkAsync(new WorkAsyncInfo
            {
                Message = "Loading entities and relationships...",
                Work = (worker, args) =>
                {
                    args.Result = _metadataService.GetAllMetadata(Service);
                },
                PostWorkCallBack = (args) =>
                {
                    if (args.Error != null)
                    {
                        MessageBox.Show(args.Error.Message, "Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    var result = (Tuple<List<Tuple<string, string>>, List<RelationshipInfo>>)args.Result;
                    _allEntities = result.Item1;
                    _allRelationships = result.Item2;

                    var ntoNEntities = new HashSet<string>();
                    foreach (var rel in _allRelationships)
                    {
                        ntoNEntities.Add(rel.Entity1LogicalName);
                        ntoNEntities.Add(rel.Entity2LogicalName);
                    }

                    var filteredEntities = _allEntities
                        .Where(e => ntoNEntities.Contains(e.Item1))
                        .ToList();

                    _suppressEntityChanged = true;
                    cmbEntity1.Items.Clear();
                    cmbEntity2.Items.Clear();

                    foreach (var entity in filteredEntities)
                    {
                        cmbEntity1.Items.Add($"{entity.Item2} ({entity.Item1})");
                    }

                    cmbEntity1.Tag = filteredEntities;
                    cmbEntity1.Enabled = true;
                    cmbEntity2.Enabled = false;
                    _suppressEntityChanged = false;

                    AppendLog($"Loaded {filteredEntities.Count} entities with N:N relationships ({_allRelationships.Count} relationships).");
                }
            });
        }

        private void cmbEntity_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_suppressEntityChanged) return;

            var changedCombo = (ComboBox)sender;
            var otherCombo = changedCombo == cmbEntity1 ? cmbEntity2 : cmbEntity1;

            if (changedCombo.SelectedIndex < 0) return;

            var sourceEntities = changedCombo.Tag as List<Tuple<string, string>>;
            if (sourceEntities == null) return;

            var selectedEntity = sourceEntities[changedCombo.SelectedIndex].Item1;

            var relatedEntityNames = new HashSet<string>();
            foreach (var rel in _allRelationships)
            {
                if (rel.Entity1LogicalName == selectedEntity)
                    relatedEntityNames.Add(rel.Entity2LogicalName);
                if (rel.Entity2LogicalName == selectedEntity)
                    relatedEntityNames.Add(rel.Entity1LogicalName);
            }

            var filteredEntities = _allEntities
                .Where(ent => relatedEntityNames.Contains(ent.Item1))
                .ToList();

            _suppressEntityChanged = true;
            otherCombo.Items.Clear();
            foreach (var entity in filteredEntities)
            {
                otherCombo.Items.Add($"{entity.Item2} ({entity.Item1})");
            }
            otherCombo.Tag = filteredEntities;
            otherCombo.Enabled = true;

            if (filteredEntities.Count == 1)
                otherCombo.SelectedIndex = 0;
            _suppressEntityChanged = false;

            bool bothSelected = cmbEntity1.SelectedIndex >= 0 && cmbEntity2.SelectedIndex >= 0;

            if (bothSelected)
            {
                PopulateRelationships();
            }
            else
            {
                cmbRelationship.Items.Clear();
                cmbRelationship.Enabled = false;
                UpdateStartButton();
            }
        }

        private void PopulateRelationships()
        {
            var entities1 = cmbEntity1.Tag as List<Tuple<string, string>>;
            var entities2 = cmbEntity2.Tag as List<Tuple<string, string>>;
            if (entities1 == null || entities2 == null) return;

            var entity1 = entities1[cmbEntity1.SelectedIndex].Item1;
            var entity2 = entities2[cmbEntity2.SelectedIndex].Item1;

            var matching = _allRelationships.Where(rel =>
                (rel.Entity1LogicalName == entity1 && rel.Entity2LogicalName == entity2) ||
                (rel.Entity1LogicalName == entity2 && rel.Entity2LogicalName == entity1))
                .ToList();

            cmbRelationship.Items.Clear();
            foreach (var rel in matching)
            {
                cmbRelationship.Items.Add(rel);
            }

            cmbRelationship.Enabled = matching.Count > 0;

            if (matching.Count == 1)
                cmbRelationship.SelectedIndex = 0;
            else if (matching.Count == 0)
                AppendLog($"No N:N relationships found between {entity1} and {entity2}.");
            else
                AppendLog($"Found {matching.Count} N:N relationships.");

            UpdateStartButton();
        }

        #endregion

        private void cmbRelationship_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateStartButton();
        }

        #region CSV Data Source

        private void btnBrowseCsv_Click(object sender, EventArgs e)
        {
            using (var ofd = new OpenFileDialog())
            {
                ofd.Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*";
                ofd.Title = "Select CSV file with GUID pairs";

                if (ofd.ShowDialog() != DialogResult.OK) return;

                txtCsvPath.Text = ofd.FileName;

                try
                {
                    _loadedPairs = _dataSourceService.LoadFromCsv(ofd.FileName);

                    dgvCsvPreview.Rows.Clear();
                    var previewCount = Math.Min(_loadedPairs.Count, 100);
                    for (int i = 0; i < previewCount; i++)
                    {
                        dgvCsvPreview.Rows.Add(_loadedPairs[i].Guid1.ToString(), _loadedPairs[i].Guid2.ToString());
                    }

                    lblCsvCount.Text = $"{_loadedPairs.Count:N0} pairs loaded (showing first {previewCount}).";
                    AppendLog($"Loaded {_loadedPairs.Count:N0} pairs from CSV.");
                    UpdateStartButton();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "CSV Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        #endregion

        #region FetchXML Data Source

        private void btnPreviewFetchXml_Click(object sender, EventArgs e)
        {
            ExecuteMethod(PreviewFetchXml);
        }

        private void PreviewFetchXml()
        {
            var fetchXml = txtFetchXml.Text.Trim();

            if (string.IsNullOrEmpty(fetchXml))
            {
                MessageBox.Show("Please enter a FetchXML query that returns two ID attributes.",
                    "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            WorkAsync(new WorkAsyncInfo
            {
                Message = "Executing FetchXML query...",
                Work = (worker, args) =>
                {
                    args.Result = _dataSourceService.LoadFromFetchXml(Service, (string)args.Argument);
                },
                AsyncArgument = fetchXml,
                PostWorkCallBack = (args) =>
                {
                    if (args.Error != null)
                    {
                        MessageBox.Show(args.Error.Message, "FetchXML Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    _loadedPairs = (List<AssociationPair>)args.Result;
                    lblFetchXmlCount.Text = $"{_loadedPairs.Count:N0} pairs found (deduplicated).";
                    AppendLog($"FetchXML returned {_loadedPairs.Count:N0} pairs.");

                    if (_loadedPairs.Count > 1_000_000)
                    {
                        MessageBox.Show(
                            $"Warning: {_loadedPairs.Count:N0} pairs generated. This may take a long time.",
                            "Large Dataset", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }

                    UpdateStartButton();
                }
            });
        }

        #endregion

        #region Association Execution

        private void btnStart_Click(object sender, EventArgs e)
        {
            ExecuteMethod(StartAssociation);
        }

        private void StartAssociation()
        {
            if (_loadedPairs == null || _loadedPairs.Count == 0)
            {
                MessageBox.Show("No pairs loaded. Load data from CSV or FetchXML first.",
                    "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (cmbRelationship.SelectedItem == null)
            {
                MessageBox.Show("Please select an N:N relationship.",
                    "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var relationship = (RelationshipInfo)cmbRelationship.SelectedItem;
            var parallelism = (int)nudParallelism.Value;
            var bypassPlugins = chkBypassPlugins.Checked;
            var verboseLogging = chkVerboseLog.Checked;
            var maxRetries = (int)nudRetries.Value;

            var resumeDir = Path.Combine(Path.GetTempPath(), "SpeedyNtoN");
            var resumePath = Path.Combine(resumeDir, $"resume_{relationship.SchemaName}.csv");

            // Check for existing resume file
            if (File.Exists(resumePath))
            {
                var lineCount = File.ReadAllLines(resumePath).Length;
                if (lineCount > 0)
                {
                    var result = MessageBox.Show(
                        $"A previous run for '{relationship.SchemaName}' has {lineCount:N0} completed pairs tracked.\n\n" +
                        "Yes = Resume (skip completed pairs)\n" +
                        "No = Start fresh (clear history)\n" +
                        "Cancel = Abort",
                        "Resume Previous Run?",
                        MessageBoxButtons.YesNoCancel,
                        MessageBoxIcon.Question);

                    if (result == DialogResult.Cancel)
                        return;

                    if (result == DialogResult.No)
                    {
                        File.Delete(resumePath);
                        AppendLog("Cleared resume history. Starting fresh.");
                    }
                    else
                    {
                        AppendLog($"Resuming -- {lineCount:N0} previously completed pairs will be skipped.");
                    }
                }
            }

            // Capture in local variable for thread safety
            var pairsToProcess = _loadedPairs;

            // Reset progress
            progressBar.Value = 0;
            progressBar.Maximum = pairsToProcess.Count;
            lblProgress.Text = $"0 / {pairsToProcess.Count:N0} completed";
            lblDuplicates.Text = "0 duplicates skipped";
            lblErrors.Text = "0 errors";

            _isRunning = true;
            btnStart.Enabled = false;
            btnStop.Enabled = true;
            grpRelationship.Enabled = false;
            tabDataSource.Enabled = false;

            _cts = new CancellationTokenSource();

            _engine.ProgressUpdated += OnProgressUpdated;
            _engine.LogMessage += OnLogMessage;

            AppendLog($"Starting association: {pairsToProcess.Count:N0} pairs, relationship: {relationship.SchemaName}, parallelism: {parallelism}");

            Task.Run(async () =>
            {
                try
                {
                    await _engine.RunAsync(Service, pairsToProcess, relationship,
                        parallelism, resumePath, bypassPlugins, verboseLogging, maxRetries, _cts.Token);
                }
                catch (OperationCanceledException)
                {
                    OnLogMessage("Operation cancelled by user.");
                }
                catch (Exception ex)
                {
                    OnLogMessage($"Fatal error: {ex.Message}");
                }
                finally
                {
                    _engine.ProgressUpdated -= OnProgressUpdated;
                    _engine.LogMessage -= OnLogMessage;

                    BeginInvoke(new Action(() =>
                    {
                        _isRunning = false;
                        btnStart.Enabled = true;
                        btnStop.Enabled = false;
                        grpRelationship.Enabled = true;
                        tabDataSource.Enabled = true;
                    }));
                }
            });
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            _cts?.Cancel();
            btnStop.Enabled = false;
            AppendLog("Stopping... (waiting for in-flight requests to complete)");
        }

        private void OnProgressUpdated(int completed, int duplicates, int errors, int total)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => OnProgressUpdated(completed, duplicates, errors, total)));
                return;
            }

            if (total > 0)
            {
                progressBar.Maximum = total;
                progressBar.Value = Math.Min(completed, total);
            }

            lblProgress.Text = $"{completed:N0} / {total:N0} completed";
            lblDuplicates.Text = $"{duplicates:N0} duplicates skipped";
            lblErrors.Text = $"{errors:N0} errors";
        }

        private void OnLogMessage(string message)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => OnLogMessage(message)));
                return;
            }

            AppendLog(message);
        }

        #endregion

        #region Helpers

        private void AppendLog(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            txtLog.AppendText($"[{timestamp}] {message}\r\n");
        }

        private void UpdateStartButton()
        {
            btnStart.Enabled = Service != null &&
                               !_isRunning &&
                               _loadedPairs != null &&
                               _loadedPairs.Count > 0 &&
                               cmbRelationship.SelectedItem != null;
        }

        #endregion
    }
}
