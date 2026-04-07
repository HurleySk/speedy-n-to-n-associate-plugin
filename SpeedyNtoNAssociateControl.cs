using McTools.Xrm.Connection;
using Microsoft.Xrm.Sdk;
using SpeedyNtoNAssociatePlugin.Models;
using SpeedyNtoNAssociatePlugin.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
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
        private readonly SqlDataSourceService _sqlDataSourceService = new SqlDataSourceService();
        private readonly AssociationEngine _engine = new AssociationEngine();

        // XML syntax highlighting
        private static readonly Regex XmlPunctuationRegex = new Regex(@"[<>/=]", RegexOptions.Compiled);
        private static readonly Regex XmlTagRegex = new Regex(@"(?<=</?)\w[\w\-]*", RegexOptions.Compiled);
        private static readonly Regex XmlAttributeRegex = new Regex(@"\b(\w[\w\-]*)(?=\s*=)", RegexOptions.Compiled);
        private static readonly Regex XmlValueRegex = new Regex("\"[^\"]*\"", RegexOptions.Compiled);

        private static readonly Color XmlDefaultColor = Color.Black;
        private static readonly Color XmlPunctuationColor = Color.Gray;
        private static readonly Color XmlTagColor = Color.Blue;
        private static readonly Color XmlAttributeColor = Color.FromArgb(163, 21, 21);
        private static readonly Color XmlValueColor = Color.Red;

        // SQL syntax highlighting
        private static readonly Regex SqlKeywordRegex = new Regex(
            @"\b(SELECT|FROM|WHERE|JOIN|LEFT|RIGHT|INNER|OUTER|CROSS|ON|AND|OR|NOT|IN|EXISTS|BETWEEN|LIKE|IS|NULL|AS|DISTINCT|TOP|ORDER|BY|GROUP|HAVING|UNION|ALL|CASE|WHEN|THEN|ELSE|END|CAST|CONVERT|COUNT|SUM|AVG|MIN|MAX|COALESCE|ISNULL)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex SqlStringRegex = new Regex(@"'[^']*'", RegexOptions.Compiled);
        private static readonly Regex SqlNumberRegex = new Regex(@"\b\d+(\.\d+)?\b", RegexOptions.Compiled);
        private static readonly Regex SqlCommentRegex = new Regex(@"--[^\r\n]*", RegexOptions.Compiled);
        private static readonly Regex SqlOperatorRegex = new Regex(@"[=<>!]+|[,;().]", RegexOptions.Compiled);

        private static readonly Color SqlDefaultColor = Color.Black;
        private static readonly Color SqlKeywordColor = Color.Blue;
        private static readonly Color SqlStringColor = Color.FromArgb(163, 21, 21);
        private static readonly Color SqlNumberColor = Color.DarkCyan;
        private static readonly Color SqlCommentColor = Color.Green;
        private static readonly Color SqlOperatorColor = Color.Gray;

        private List<AssociationPair> _loadedPairs;
        private string _csvFilePath;
        private string _fetchXmlText;
        private List<Tuple<string, string>> _allEntities;
        private List<RelationshipInfo> _allRelationships;
        private CancellationTokenSource _cts;
        private bool _isRunning;
        private bool _suppressEntityChanged;
        private readonly System.Windows.Forms.Timer _colorizeTimer;
        private bool _suppressColorize;
        private readonly System.Windows.Forms.Timer _sqlColorizeTimer;
        private bool _suppressSqlColorize;

        public SpeedyNtoNAssociateControl()
        {
            InitializeComponent();
            _colorizeTimer = new System.Windows.Forms.Timer { Interval = 300 };
            _colorizeTimer.Tick += (s, e) => { _colorizeTimer.Stop(); FormatAndColorize(); };
            _sqlColorizeTimer = new System.Windows.Forms.Timer { Interval = 300 };
            _sqlColorizeTimer.Tick += (s, e) => { _sqlColorizeTimer.Stop(); ColorizeSql(); };
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
            _csvFilePath = null;
            _fetchXmlText = null;

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
                    // Load for preview only (first 100 rows shown)
                    _loadedPairs = _dataSourceService.LoadFromCsv(ofd.FileName);
                    _csvFilePath = ofd.FileName;

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

        private void txtFetchXml_TextChanged(object sender, EventArgs e)
        {
            if (_suppressColorize) return;
            _colorizeTimer.Stop();
            _colorizeTimer.Start();
        }

        private void FormatAndColorize()
        {
            var xml = txtFetchXml.Text.Trim();
            if (string.IsNullOrEmpty(xml)) return;

            try
            {
                var doc = XDocument.Parse(xml);
                _suppressColorize = true;
                txtFetchXml.Text = doc.ToString();
                _suppressColorize = false;
                ColorizeXml();
            }
            catch
            {
                // If XML is invalid, just colorize what's there
                ColorizeXml();
            }
        }

        private void btnFormatXml_Click(object sender, EventArgs e)
        {
            var xml = txtFetchXml.Text.Trim();
            if (string.IsNullOrEmpty(xml))
            {
                MessageBox.Show("No FetchXML to validate.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                var doc = XDocument.Parse(xml);
                _suppressColorize = true;
                txtFetchXml.Text = doc.ToString();
                _suppressColorize = false;
                ColorizeXml();
                MessageBox.Show("FetchXML is valid.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                AppendLog("FetchXML validated successfully.");
            }
            catch (System.Xml.XmlException ex)
            {
                MessageBox.Show($"Invalid XML at line {ex.LineNumber}, position {ex.LinePosition}:\n\n{ex.Message}",
                    "XML Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ColorizeXml()
        {
            var text = txtFetchXml.Text;
            if (string.IsNullOrWhiteSpace(text)) return;

            _suppressColorize = true;
            var selStart = txtFetchXml.SelectionStart;
            var selLength = txtFetchXml.SelectionLength;

            txtFetchXml.SelectAll();
            txtFetchXml.SelectionColor = XmlDefaultColor;

            foreach (Match m in XmlPunctuationRegex.Matches(text))
            {
                txtFetchXml.Select(m.Index, m.Length);
                txtFetchXml.SelectionColor = XmlPunctuationColor;
            }

            foreach (Match m in XmlTagRegex.Matches(text))
            {
                txtFetchXml.Select(m.Index, m.Length);
                txtFetchXml.SelectionColor = XmlTagColor;
            }

            foreach (Match m in XmlAttributeRegex.Matches(text))
            {
                txtFetchXml.Select(m.Groups[1].Index, m.Groups[1].Length);
                txtFetchXml.SelectionColor = XmlAttributeColor;
            }

            foreach (Match m in XmlValueRegex.Matches(text))
            {
                txtFetchXml.Select(m.Index, m.Length);
                txtFetchXml.SelectionColor = XmlValueColor;
            }

            txtFetchXml.Select(selStart, selLength);
            txtFetchXml.SelectionColor = XmlDefaultColor;
            _suppressColorize = false;
        }

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

            // Get entity names from selected relationship for smart filtering
            string entity1Name = null, entity2Name = null;
            var rel = cmbRelationship.SelectedItem as RelationshipInfo;
            if (rel != null)
            {
                entity1Name = rel.Entity1LogicalName;
                entity2Name = rel.Entity2LogicalName;
            }

            WorkAsync(new WorkAsyncInfo
            {
                Message = "Executing FetchXML query...",
                Work = (worker, args) =>
                {
                    var param = (Tuple<string, string, string>)args.Argument;
                    args.Result = _dataSourceService.LoadFromFetchXml(Service, param.Item1, param.Item2, param.Item3);
                },
                AsyncArgument = Tuple.Create(fetchXml, entity1Name ?? "", entity2Name ?? ""),
                PostWorkCallBack = (args) =>
                {
                    if (args.Error != null)
                    {
                        MessageBox.Show(args.Error.Message, "FetchXML Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    var fetchResult = (Tuple<List<AssociationPair>, int>)args.Result;
                    _loadedPairs = fetchResult.Item1;
                    _fetchXmlText = fetchXml;
                    var skipped = fetchResult.Item2;

                    var countText = $"{_loadedPairs.Count:N0} pairs found (deduplicated).";
                    if (skipped > 0)
                        countText += $" {skipped:N0} rows skipped (GUIDs did not match selected entities).";

                    lblFetchXmlCount.Text = countText;

                    dgvFetchPreview.Rows.Clear();
                    var previewCount = Math.Min(_loadedPairs.Count, 100);
                    for (int i = 0; i < previewCount; i++)
                    {
                        dgvFetchPreview.Rows.Add(_loadedPairs[i].Guid1.ToString(), _loadedPairs[i].Guid2.ToString());
                    }

                    AppendLog($"FetchXML returned {_loadedPairs.Count:N0} pairs.");
                    if (skipped > 0)
                        AppendLog($"Skipped {skipped:N0} rows -- GUIDs did not match {entity1Name} or {entity2Name}.");

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

        #region SQL Data Source

        private void txtSqlQuery_TextChanged(object sender, EventArgs e)
        {
            if (_suppressSqlColorize) return;
            _sqlColorizeTimer.Stop();
            _sqlColorizeTimer.Start();
        }

        private void ColorizeSql()
        {
            var text = txtSqlQuery.Text;
            if (string.IsNullOrWhiteSpace(text)) return;

            _suppressSqlColorize = true;
            var selStart = txtSqlQuery.SelectionStart;
            var selLength = txtSqlQuery.SelectionLength;

            txtSqlQuery.SelectAll();
            txtSqlQuery.SelectionColor = SqlDefaultColor;

            foreach (Match m in SqlOperatorRegex.Matches(text))
            {
                txtSqlQuery.Select(m.Index, m.Length);
                txtSqlQuery.SelectionColor = SqlOperatorColor;
            }

            foreach (Match m in SqlKeywordRegex.Matches(text))
            {
                txtSqlQuery.Select(m.Index, m.Length);
                txtSqlQuery.SelectionColor = SqlKeywordColor;
            }

            foreach (Match m in SqlNumberRegex.Matches(text))
            {
                txtSqlQuery.Select(m.Index, m.Length);
                txtSqlQuery.SelectionColor = SqlNumberColor;
            }

            foreach (Match m in SqlStringRegex.Matches(text))
            {
                txtSqlQuery.Select(m.Index, m.Length);
                txtSqlQuery.SelectionColor = SqlStringColor;
            }

            foreach (Match m in SqlCommentRegex.Matches(text))
            {
                txtSqlQuery.Select(m.Index, m.Length);
                txtSqlQuery.SelectionColor = SqlCommentColor;
            }

            txtSqlQuery.Select(selStart, selLength);
            txtSqlQuery.SelectionColor = SqlDefaultColor;
            _suppressSqlColorize = false;
        }

        private void btnPreviewSql_Click(object sender, EventArgs e)
        {
            ExecuteMethod(PreviewSql);
        }

        private void PreviewSql()
        {
            var sql = txtSqlQuery.Text.Trim();

            if (string.IsNullOrEmpty(sql))
            {
                MessageBox.Show("Please enter a SQL query that returns two GUID columns.",
                    "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string entity1Name = null, entity2Name = null;
            var rel = cmbRelationship.SelectedItem as RelationshipInfo;
            if (rel != null)
            {
                entity1Name = rel.Entity1LogicalName;
                entity2Name = rel.Entity2LogicalName;
            }

            WorkAsync(new WorkAsyncInfo
            {
                Message = "Executing SQL query...",
                Work = (worker, args) =>
                {
                    var param = (Tuple<string, string, string>)args.Argument;
                    args.Result = _sqlDataSourceService.LoadFromSql(Service, param.Item1, param.Item2, param.Item3);
                },
                AsyncArgument = Tuple.Create(sql, entity1Name ?? "", entity2Name ?? ""),
                PostWorkCallBack = (args) =>
                {
                    if (args.Error != null)
                    {
                        MessageBox.Show(args.Error.Message, "SQL Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    var sqlResult = (Tuple<List<AssociationPair>, int>)args.Result;
                    _loadedPairs = sqlResult.Item1;
                    var skipped = sqlResult.Item2;

                    var countText = $"{_loadedPairs.Count:N0} pairs found (deduplicated).";
                    if (skipped > 0)
                        countText += $" {skipped:N0} rows skipped.";

                    lblSqlCount.Text = countText;

                    dgvSqlPreview.Rows.Clear();
                    var previewCount = Math.Min(_loadedPairs.Count, 100);
                    for (int i = 0; i < previewCount; i++)
                    {
                        dgvSqlPreview.Rows.Add(_loadedPairs[i].Guid1.ToString(), _loadedPairs[i].Guid2.ToString());
                    }

                    AppendLog($"SQL returned {_loadedPairs.Count:N0} pairs.");
                    if (skipped > 0)
                        AppendLog($"Skipped {skipped:N0} rows.");

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
                MessageBox.Show("No pairs loaded. Load data from CSV, FetchXML, or SQL first.",
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
            var batchSize = (int)nudBatchSize.Value;

            var resumeDir = Path.Combine(Path.GetTempPath(), "SpeedyNtoN");
            var resumePath = Path.Combine(resumeDir, $"resume_{relationship.SchemaName}.db");

            // Check for existing resume database
            var resumeTracker = new ResumeTracker(resumePath);
            bool startFresh = false;

            if (File.Exists(resumePath))
            {
                resumeTracker.Open();
                var completedCount = resumeTracker.GetCompletedCount();

                if (completedCount > 0)
                {
                    var result = MessageBox.Show(
                        $"A previous run for '{relationship.SchemaName}' has {completedCount:N0} completed pairs tracked.\n\n" +
                        "Yes = Resume (skip completed pairs)\n" +
                        "No = Start fresh (clear history)\n" +
                        "Cancel = Abort",
                        "Resume Previous Run?",
                        MessageBoxButtons.YesNoCancel,
                        MessageBoxIcon.Question);

                    if (result == DialogResult.Cancel)
                    {
                        resumeTracker.Dispose();
                        return;
                    }

                    if (result == DialogResult.No)
                    {
                        startFresh = true;
                        resumeTracker.DeleteDatabase();
                        AppendLog("Cleared resume history. Starting fresh.");
                    }
                    else
                    {
                        AppendLog($"Resuming -- {completedCount:N0} previously completed pairs will be skipped.");
                    }
                }
            }

            if (startFresh || !File.Exists(resumePath))
            {
                resumeTracker = new ResumeTracker(resumePath);
                resumeTracker.Open();
            }

            // Build streaming data source
            IEnumerable<AssociationPair> pairsSource;
            string entity1Name = relationship.Entity1LogicalName;
            string entity2Name = relationship.Entity2LogicalName;

            bool isCsvTab = tabDataSource.SelectedIndex == 0;
            if (isCsvTab && !string.IsNullOrEmpty(_csvFilePath))
            {
                pairsSource = _dataSourceService.StreamFromCsv(_csvFilePath);
                AppendLog($"Streaming pairs from CSV: {_csvFilePath}");
            }
            else if (!isCsvTab && !string.IsNullOrEmpty(_fetchXmlText))
            {
                pairsSource = _dataSourceService.StreamFromFetchXml(Service, _fetchXmlText, entity1Name, entity2Name);
                AppendLog("Streaming pairs from FetchXML.");
            }
            else
            {
                // Fallback to loaded pairs if no streaming source available
                pairsSource = _loadedPairs;
            }

            // Reset progress — total unknown until producer finishes
            progressBar.Style = ProgressBarStyle.Marquee;
            progressBar.Value = 0;
            lblProgress.Text = "0 completed (loading...)";
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

            AppendLog($"Starting association: relationship: {relationship.SchemaName}, parallelism: {parallelism}, batch size: {batchSize}");

            var capturedResumeTracker = resumeTracker;

            Task.Run(async () =>
            {
                try
                {
                    await _engine.RunAsync(Service, pairsSource, relationship,
                        parallelism, capturedResumeTracker, bypassPlugins, verboseLogging,
                        maxRetries, batchSize, _cts.Token);
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
                    capturedResumeTracker.Dispose();
                    _engine.ProgressUpdated -= OnProgressUpdated;
                    _engine.LogMessage -= OnLogMessage;

                    BeginInvoke(new Action(() =>
                    {
                        _isRunning = false;
                        btnStart.Enabled = true;
                        btnStop.Enabled = false;
                        grpRelationship.Enabled = true;
                        tabDataSource.Enabled = true;
                        progressBar.Style = ProgressBarStyle.Blocks;
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

        private void OnProgressUpdated(int completed, int duplicates, int errors, int? total)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => OnProgressUpdated(completed, duplicates, errors, total)));
                return;
            }

            if (total.HasValue && total.Value > 0)
            {
                if (progressBar.Style != ProgressBarStyle.Blocks)
                    progressBar.Style = ProgressBarStyle.Blocks;
                progressBar.Maximum = total.Value;
                progressBar.Value = Math.Min(completed, total.Value);
                lblProgress.Text = $"{completed:N0} / {total.Value:N0} completed";
            }
            else
            {
                lblProgress.Text = $"{completed:N0} completed (loading...)";
            }

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
