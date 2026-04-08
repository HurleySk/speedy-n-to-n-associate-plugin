namespace SpeedyNtoNAssociatePlugin
{
    partial class SpeedyNtoNAssociateControl
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _colorizeTimer?.Dispose();
                _sqlColorizeTimer?.Dispose();
                components?.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        private void InitializeComponent()
        {
            this.lblStatus = new System.Windows.Forms.Label();
            this.grpRelationship = new System.Windows.Forms.GroupBox();
            this.cmbRelationship = new System.Windows.Forms.ComboBox();
            this.lblRelationship = new System.Windows.Forms.Label();
            this.cmbEntity2 = new System.Windows.Forms.ComboBox();
            this.lblEntity2 = new System.Windows.Forms.Label();
            this.cmbEntity1 = new System.Windows.Forms.ComboBox();
            this.lblEntity1 = new System.Windows.Forms.Label();
            this.btnLoadEntities = new System.Windows.Forms.Button();
            this.tabDataSource = new System.Windows.Forms.TabControl();
            this.tabCsv = new System.Windows.Forms.TabPage();
            this.lblCsvCount = new System.Windows.Forms.Label();
            this.dgvCsvPreview = new System.Windows.Forms.DataGridView();
            this.colGuid1 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colGuid2 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.btnBrowseCsv = new System.Windows.Forms.Button();
            this.txtCsvPath = new System.Windows.Forms.TextBox();
            this.tabFetchXml = new System.Windows.Forms.TabPage();
            this.lblFetchInstructions = new System.Windows.Forms.Label();
            this.splitFetch = new System.Windows.Forms.SplitContainer();
            this.txtFetchXml = new System.Windows.Forms.RichTextBox();
            this.dgvFetchPreview = new System.Windows.Forms.DataGridView();
            this.colFetchGuid1 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colFetchGuid2 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.lblFetchXmlCount = new System.Windows.Forms.Label();
            this.btnPreviewFetchXml = new System.Windows.Forms.Button();
            this.tabSql = new System.Windows.Forms.TabPage();
            this.lblSqlInstructions = new System.Windows.Forms.Label();
            this.splitSql = new System.Windows.Forms.SplitContainer();
            this.txtSqlQuery = new System.Windows.Forms.RichTextBox();
            this.dgvSqlPreview = new System.Windows.Forms.DataGridView();
            this.colSqlGuid1 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colSqlGuid2 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.btnPreviewSql = new System.Windows.Forms.Button();
            this.lblSqlCount = new System.Windows.Forms.Label();
            this.grpSettings = new System.Windows.Forms.GroupBox();
            this.chkBypassPlugins = new System.Windows.Forms.CheckBox();
            this.chkVerboseLog = new System.Windows.Forms.CheckBox();
            this.nudRetries = new System.Windows.Forms.NumericUpDown();
            this.lblRetries = new System.Windows.Forms.Label();
            this.nudParallelism = new System.Windows.Forms.NumericUpDown();
            this.lblParallelism = new System.Windows.Forms.Label();
            this.grpProgress = new System.Windows.Forms.GroupBox();
            this.lblErrors = new System.Windows.Forms.Label();
            this.lblDuplicates = new System.Windows.Forms.Label();
            this.lblProgress = new System.Windows.Forms.Label();
            this.progressBar = new System.Windows.Forms.ProgressBar();
            this.btnStart = new System.Windows.Forms.Button();
            this.btnStop = new System.Windows.Forms.Button();
            this.txtLog = new System.Windows.Forms.TextBox();
            this.grpRelationship.SuspendLayout();
            this.tabDataSource.SuspendLayout();
            this.tabCsv.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvCsvPreview)).BeginInit();
            this.tabFetchXml.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitFetch)).BeginInit();
            this.splitFetch.Panel1.SuspendLayout();
            this.splitFetch.Panel2.SuspendLayout();
            this.splitFetch.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvFetchPreview)).BeginInit();
            this.tabSql.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitSql)).BeginInit();
            this.splitSql.Panel1.SuspendLayout();
            this.splitSql.Panel2.SuspendLayout();
            this.splitSql.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvSqlPreview)).BeginInit();
            this.grpSettings.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.nudRetries)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudParallelism)).BeginInit();
            this.grpProgress.SuspendLayout();
            this.SuspendLayout();
            //
            // lblStatus
            //
            this.lblStatus.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.lblStatus.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F);
            this.lblStatus.ForeColor = System.Drawing.Color.Gray;
            this.lblStatus.Location = new System.Drawing.Point(12, 8);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(776, 20);
            this.lblStatus.TabIndex = 0;
            this.lblStatus.Text = "Not connected. Use the connection button above to connect.";
            //
            // grpRelationship
            //
            this.grpRelationship.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.grpRelationship.Controls.Add(this.cmbRelationship);
            this.grpRelationship.Controls.Add(this.lblRelationship);
            this.grpRelationship.Controls.Add(this.cmbEntity2);
            this.grpRelationship.Controls.Add(this.lblEntity2);
            this.grpRelationship.Controls.Add(this.cmbEntity1);
            this.grpRelationship.Controls.Add(this.lblEntity1);
            this.grpRelationship.Controls.Add(this.btnLoadEntities);
            this.grpRelationship.Location = new System.Drawing.Point(12, 32);
            this.grpRelationship.Name = "grpRelationship";
            this.grpRelationship.Size = new System.Drawing.Size(776, 80);
            this.grpRelationship.TabIndex = 1;
            this.grpRelationship.TabStop = false;
            this.grpRelationship.Text = "Relationship Configuration";
            //
            // btnLoadEntities
            //
            this.btnLoadEntities.Enabled = false;
            this.btnLoadEntities.Location = new System.Drawing.Point(10, 22);
            this.btnLoadEntities.Name = "btnLoadEntities";
            this.btnLoadEntities.Size = new System.Drawing.Size(100, 23);
            this.btnLoadEntities.TabIndex = 0;
            this.btnLoadEntities.Text = "Load Entities";
            this.btnLoadEntities.UseVisualStyleBackColor = true;
            this.btnLoadEntities.Click += new System.EventHandler(this.btnLoadEntities_Click);
            //
            // lblEntity1
            //
            this.lblEntity1.AutoSize = true;
            this.lblEntity1.Location = new System.Drawing.Point(120, 26);
            this.lblEntity1.Name = "lblEntity1";
            this.lblEntity1.Size = new System.Drawing.Size(46, 13);
            this.lblEntity1.TabIndex = 1;
            this.lblEntity1.Text = "Entity 1:";
            //
            // cmbEntity1
            //
            this.cmbEntity1.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.SuggestAppend;
            this.cmbEntity1.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
            this.cmbEntity1.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDown;
            this.cmbEntity1.Enabled = false;
            this.cmbEntity1.Location = new System.Drawing.Point(172, 22);
            this.cmbEntity1.Name = "cmbEntity1";
            this.cmbEntity1.Size = new System.Drawing.Size(170, 21);
            this.cmbEntity1.TabIndex = 2;
            this.cmbEntity1.SelectedIndexChanged += new System.EventHandler(this.cmbEntity_SelectedIndexChanged);
            //
            // lblEntity2
            //
            this.lblEntity2.AutoSize = true;
            this.lblEntity2.Location = new System.Drawing.Point(350, 26);
            this.lblEntity2.Name = "lblEntity2";
            this.lblEntity2.Size = new System.Drawing.Size(46, 13);
            this.lblEntity2.TabIndex = 3;
            this.lblEntity2.Text = "Entity 2:";
            //
            // cmbEntity2
            //
            this.cmbEntity2.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.SuggestAppend;
            this.cmbEntity2.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
            this.cmbEntity2.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDown;
            this.cmbEntity2.Enabled = false;
            this.cmbEntity2.Location = new System.Drawing.Point(402, 22);
            this.cmbEntity2.Name = "cmbEntity2";
            this.cmbEntity2.Size = new System.Drawing.Size(170, 21);
            this.cmbEntity2.TabIndex = 4;
            this.cmbEntity2.SelectedIndexChanged += new System.EventHandler(this.cmbEntity_SelectedIndexChanged);
            //
            // lblRelationship
            //
            this.lblRelationship.AutoSize = true;
            this.lblRelationship.Location = new System.Drawing.Point(10, 52);
            this.lblRelationship.Name = "lblRelationship";
            this.lblRelationship.Size = new System.Drawing.Size(71, 13);
            this.lblRelationship.TabIndex = 5;
            this.lblRelationship.Text = "Relationship:";
            //
            // cmbRelationship
            //
            this.cmbRelationship.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.cmbRelationship.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbRelationship.Enabled = false;
            this.cmbRelationship.Location = new System.Drawing.Point(87, 48);
            this.cmbRelationship.Name = "cmbRelationship";
            this.cmbRelationship.Size = new System.Drawing.Size(680, 21);
            this.cmbRelationship.TabIndex = 6;
            this.cmbRelationship.SelectedIndexChanged += new System.EventHandler(this.cmbRelationship_SelectedIndexChanged);
            //
            // tabDataSource
            //
            this.tabDataSource.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.tabDataSource.Controls.Add(this.tabCsv);
            this.tabDataSource.Controls.Add(this.tabFetchXml);
            this.tabDataSource.Controls.Add(this.tabSql);
            this.tabDataSource.Location = new System.Drawing.Point(12, 118);
            this.tabDataSource.Name = "tabDataSource";
            this.tabDataSource.SelectedIndex = 0;
            this.tabDataSource.Size = new System.Drawing.Size(776, 220);
            this.tabDataSource.TabIndex = 2;
            //
            // tabCsv
            //
            this.tabCsv.Controls.Add(this.lblCsvCount);
            this.tabCsv.Controls.Add(this.dgvCsvPreview);
            this.tabCsv.Controls.Add(this.btnBrowseCsv);
            this.tabCsv.Controls.Add(this.txtCsvPath);
            this.tabCsv.Location = new System.Drawing.Point(4, 22);
            this.tabCsv.Name = "tabCsv";
            this.tabCsv.Padding = new System.Windows.Forms.Padding(6);
            this.tabCsv.Size = new System.Drawing.Size(768, 194);
            this.tabCsv.TabIndex = 0;
            this.tabCsv.Text = "CSV Import";
            this.tabCsv.UseVisualStyleBackColor = true;
            //
            // txtCsvPath
            //
            this.txtCsvPath.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.txtCsvPath.Location = new System.Drawing.Point(9, 9);
            this.txtCsvPath.Name = "txtCsvPath";
            this.txtCsvPath.ReadOnly = true;
            this.txtCsvPath.Size = new System.Drawing.Size(570, 20);
            this.txtCsvPath.TabIndex = 0;
            //
            // btnBrowseCsv
            //
            this.btnBrowseCsv.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnBrowseCsv.Location = new System.Drawing.Point(585, 7);
            this.btnBrowseCsv.Name = "btnBrowseCsv";
            this.btnBrowseCsv.Size = new System.Drawing.Size(174, 23);
            this.btnBrowseCsv.TabIndex = 1;
            this.btnBrowseCsv.Text = "Browse && Load CSV";
            this.btnBrowseCsv.UseVisualStyleBackColor = true;
            this.btnBrowseCsv.Click += new System.EventHandler(this.btnBrowseCsv_Click);
            //
            // dgvCsvPreview
            //
            this.dgvCsvPreview.AllowUserToAddRows = false;
            this.dgvCsvPreview.AllowUserToDeleteRows = false;
            this.dgvCsvPreview.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.dgvCsvPreview.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvCsvPreview.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] { this.colGuid1, this.colGuid2 });
            this.dgvCsvPreview.Location = new System.Drawing.Point(9, 35);
            this.dgvCsvPreview.Name = "dgvCsvPreview";
            this.dgvCsvPreview.ReadOnly = true;
            this.dgvCsvPreview.RowHeadersVisible = false;
            this.dgvCsvPreview.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dgvCsvPreview.Size = new System.Drawing.Size(750, 132);
            this.dgvCsvPreview.TabIndex = 3;
            //
            // colGuid1
            //
            this.colGuid1.HeaderText = "GUID 1";
            this.colGuid1.Name = "colGuid1";
            this.colGuid1.ReadOnly = true;
            // Width managed by AutoSizeColumnsMode.Fill
            //
            // colGuid2
            //
            this.colGuid2.HeaderText = "GUID 2";
            this.colGuid2.Name = "colGuid2";
            this.colGuid2.ReadOnly = true;
            // Width managed by AutoSizeColumnsMode.Fill
            //
            // lblCsvCount
            //
            this.lblCsvCount.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.lblCsvCount.AutoSize = true;
            this.lblCsvCount.Location = new System.Drawing.Point(9, 174);
            this.lblCsvCount.Name = "lblCsvCount";
            this.lblCsvCount.Size = new System.Drawing.Size(84, 13);
            this.lblCsvCount.TabIndex = 4;
            this.lblCsvCount.Text = "No file loaded.";
            //
            // tabFetchXml
            //
            this.btnFormatXml = new System.Windows.Forms.Button();
            this.tabFetchXml.Controls.Add(this.lblFetchInstructions);
            this.tabFetchXml.Controls.Add(this.splitFetch);
            this.tabFetchXml.Controls.Add(this.lblFetchXmlCount);
            this.tabFetchXml.Controls.Add(this.btnFormatXml);
            this.tabFetchXml.Controls.Add(this.btnPreviewFetchXml);
            this.tabFetchXml.Location = new System.Drawing.Point(4, 22);
            this.tabFetchXml.Name = "tabFetchXml";
            this.tabFetchXml.Padding = new System.Windows.Forms.Padding(6);
            this.tabFetchXml.Size = new System.Drawing.Size(768, 194);
            this.tabFetchXml.TabIndex = 1;
            this.tabFetchXml.Text = "FetchXML";
            this.tabFetchXml.UseVisualStyleBackColor = true;
            //
            // lblFetchInstructions
            //
            this.lblFetchInstructions.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.lblFetchInstructions.ForeColor = System.Drawing.Color.Gray;
            this.lblFetchInstructions.Location = new System.Drawing.Point(9, 6);
            this.lblFetchInstructions.Name = "lblFetchInstructions";
            this.lblFetchInstructions.Size = new System.Drawing.Size(750, 15);
            this.lblFetchInstructions.TabIndex = 0;
            this.lblFetchInstructions.Text = "Write a FetchXML that returns two ID attributes (e.g. via link-entity). Each row becomes one pair to associate.";
            //
            // splitFetch
            //
            this.splitFetch.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.splitFetch.Location = new System.Drawing.Point(9, 24);
            this.splitFetch.Name = "splitFetch";
            this.splitFetch.Orientation = System.Windows.Forms.Orientation.Horizontal;
            this.splitFetch.Size = new System.Drawing.Size(750, 137);
            this.splitFetch.SplitterDistance = 70;
            this.splitFetch.TabIndex = 1;
            this.splitFetch.Panel2MinSize = 40;
            this.splitFetch.Panel2Collapsed = true;
            //
            // splitFetch.Panel1 -- editor
            //
            this.splitFetch.Panel1.Controls.Add(this.txtFetchXml);
            //
            // splitFetch.Panel2 -- preview grid
            //
            this.splitFetch.Panel2.Controls.Add(this.dgvFetchPreview);
            //
            // txtFetchXml
            //
            this.txtFetchXml.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtFetchXml.Font = new System.Drawing.Font("Consolas", 8.25F);
            this.txtFetchXml.Name = "txtFetchXml";
            this.txtFetchXml.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Both;
            this.txtFetchXml.TabIndex = 0;
            this.txtFetchXml.WordWrap = false;
            this.txtFetchXml.AcceptsTab = true;
            this.txtFetchXml.TextChanged += new System.EventHandler(this.txtFetchXml_TextChanged);
            //
            // dgvFetchPreview
            //
            this.dgvFetchPreview.AllowUserToAddRows = false;
            this.dgvFetchPreview.AllowUserToDeleteRows = false;
            this.dgvFetchPreview.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvFetchPreview.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvFetchPreview.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] { this.colFetchGuid1, this.colFetchGuid2 });
            this.dgvFetchPreview.Name = "dgvFetchPreview";
            this.dgvFetchPreview.ReadOnly = true;
            this.dgvFetchPreview.RowHeadersVisible = false;
            this.dgvFetchPreview.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dgvFetchPreview.TabIndex = 0;
            //
            // colFetchGuid1
            //
            this.colFetchGuid1.HeaderText = "GUID 1";
            this.colFetchGuid1.Name = "colFetchGuid1";
            this.colFetchGuid1.ReadOnly = true;
            //
            // colFetchGuid2
            //
            this.colFetchGuid2.HeaderText = "GUID 2";
            this.colFetchGuid2.Name = "colFetchGuid2";
            this.colFetchGuid2.ReadOnly = true;
            //
            // btnPreviewFetchXml
            //
            this.btnPreviewFetchXml.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnPreviewFetchXml.Location = new System.Drawing.Point(9, 165);
            this.btnPreviewFetchXml.Name = "btnPreviewFetchXml";
            this.btnPreviewFetchXml.Size = new System.Drawing.Size(120, 23);
            this.btnPreviewFetchXml.TabIndex = 2;
            this.btnPreviewFetchXml.Text = "Preview Pairs";
            this.btnPreviewFetchXml.UseVisualStyleBackColor = true;
            this.btnPreviewFetchXml.Click += new System.EventHandler(this.btnPreviewFetchXml_Click);
            //
            // btnFormatXml
            //
            this.btnFormatXml.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnFormatXml.Location = new System.Drawing.Point(135, 165);
            this.btnFormatXml.Name = "btnFormatXml";
            this.btnFormatXml.Size = new System.Drawing.Size(120, 23);
            this.btnFormatXml.TabIndex = 4;
            this.btnFormatXml.Text = "Validate";
            this.btnFormatXml.UseVisualStyleBackColor = true;
            this.btnFormatXml.Click += new System.EventHandler(this.btnFormatXml_Click);
            //
            // lblFetchXmlCount
            //
            this.lblFetchXmlCount.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.lblFetchXmlCount.AutoSize = true;
            this.lblFetchXmlCount.Location = new System.Drawing.Point(261, 170);
            this.lblFetchXmlCount.Name = "lblFetchXmlCount";
            this.lblFetchXmlCount.Size = new System.Drawing.Size(0, 13);
            this.lblFetchXmlCount.TabIndex = 3;
            //
            // tabSql
            //
            this.tabSql.Controls.Add(this.lblSqlInstructions);
            this.tabSql.Controls.Add(this.splitSql);
            this.tabSql.Controls.Add(this.btnPreviewSql);
            this.tabSql.Controls.Add(this.lblSqlCount);
            this.tabSql.Location = new System.Drawing.Point(4, 22);
            this.tabSql.Name = "tabSql";
            this.tabSql.Padding = new System.Windows.Forms.Padding(6);
            this.tabSql.Size = new System.Drawing.Size(768, 194);
            this.tabSql.TabIndex = 2;
            this.tabSql.Text = "SQL Query";
            this.tabSql.UseVisualStyleBackColor = true;
            //
            // lblSqlInstructions
            //
            this.lblSqlInstructions.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.lblSqlInstructions.ForeColor = System.Drawing.Color.Gray;
            this.lblSqlInstructions.Location = new System.Drawing.Point(9, 6);
            this.lblSqlInstructions.Name = "lblSqlInstructions";
            this.lblSqlInstructions.Size = new System.Drawing.Size(750, 15);
            this.lblSqlInstructions.TabIndex = 0;
            this.lblSqlInstructions.Text = "Write a SQL SELECT that returns two GUID columns (via TDS endpoint). Each row becomes one pair.";
            //
            // splitSql
            //
            this.splitSql.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.splitSql.Location = new System.Drawing.Point(9, 24);
            this.splitSql.Name = "splitSql";
            this.splitSql.Orientation = System.Windows.Forms.Orientation.Horizontal;
            this.splitSql.Size = new System.Drawing.Size(750, 137);
            this.splitSql.SplitterDistance = 70;
            this.splitSql.TabIndex = 1;
            this.splitSql.Panel2MinSize = 40;
            this.splitSql.Panel2Collapsed = true;
            //
            // splitSql.Panel1 -- editor
            //
            this.splitSql.Panel1.Controls.Add(this.txtSqlQuery);
            //
            // splitSql.Panel2 -- preview grid
            //
            this.splitSql.Panel2.Controls.Add(this.dgvSqlPreview);
            //
            // txtSqlQuery
            //
            this.txtSqlQuery.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtSqlQuery.Font = new System.Drawing.Font("Consolas", 8.25F);
            this.txtSqlQuery.Name = "txtSqlQuery";
            this.txtSqlQuery.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Both;
            this.txtSqlQuery.TabIndex = 0;
            this.txtSqlQuery.WordWrap = false;
            this.txtSqlQuery.AcceptsTab = true;
            this.txtSqlQuery.TextChanged += new System.EventHandler(this.txtSqlQuery_TextChanged);
            //
            // dgvSqlPreview
            //
            this.dgvSqlPreview.AllowUserToAddRows = false;
            this.dgvSqlPreview.AllowUserToDeleteRows = false;
            this.dgvSqlPreview.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvSqlPreview.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvSqlPreview.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] { this.colSqlGuid1, this.colSqlGuid2 });
            this.dgvSqlPreview.Name = "dgvSqlPreview";
            this.dgvSqlPreview.ReadOnly = true;
            this.dgvSqlPreview.RowHeadersVisible = false;
            this.dgvSqlPreview.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dgvSqlPreview.TabIndex = 0;
            //
            // colSqlGuid1
            //
            this.colSqlGuid1.HeaderText = "GUID 1";
            this.colSqlGuid1.Name = "colSqlGuid1";
            this.colSqlGuid1.ReadOnly = true;
            //
            // colSqlGuid2
            //
            this.colSqlGuid2.HeaderText = "GUID 2";
            this.colSqlGuid2.Name = "colSqlGuid2";
            this.colSqlGuid2.ReadOnly = true;
            //
            // btnPreviewSql
            //
            this.btnPreviewSql.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnPreviewSql.Location = new System.Drawing.Point(9, 165);
            this.btnPreviewSql.Name = "btnPreviewSql";
            this.btnPreviewSql.Size = new System.Drawing.Size(120, 23);
            this.btnPreviewSql.TabIndex = 2;
            this.btnPreviewSql.Text = "Preview Pairs";
            this.btnPreviewSql.UseVisualStyleBackColor = true;
            this.btnPreviewSql.Click += new System.EventHandler(this.btnPreviewSql_Click);
            //
            // lblSqlCount
            //
            this.lblSqlCount.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.lblSqlCount.AutoSize = true;
            this.lblSqlCount.Location = new System.Drawing.Point(135, 170);
            this.lblSqlCount.Name = "lblSqlCount";
            this.lblSqlCount.Size = new System.Drawing.Size(0, 13);
            this.lblSqlCount.TabIndex = 3;
            //
            // grpSettings
            //
            this.grpSettings.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.nudBatchSize = new System.Windows.Forms.NumericUpDown();
            this.lblBatchSize = new System.Windows.Forms.Label();
            this.chkFireAndForget = new System.Windows.Forms.CheckBox();
            this.toolTip = new System.Windows.Forms.ToolTip();
            ((System.ComponentModel.ISupportInitialize)(this.nudBatchSize)).BeginInit();
            this.grpSettings.Controls.Add(this.chkFireAndForget);
            this.grpSettings.Controls.Add(this.nudBatchSize);
            this.grpSettings.Controls.Add(this.lblBatchSize);
            this.grpSettings.Controls.Add(this.chkVerboseLog);
            this.grpSettings.Controls.Add(this.nudRetries);
            this.grpSettings.Controls.Add(this.lblRetries);
            this.grpSettings.Controls.Add(this.chkBypassPlugins);
            this.grpSettings.Controls.Add(this.nudParallelism);
            this.grpSettings.Controls.Add(this.lblParallelism);
            this.grpSettings.Location = new System.Drawing.Point(12, 344);
            this.grpSettings.Name = "grpSettings";
            this.grpSettings.Size = new System.Drawing.Size(776, 72);
            this.grpSettings.TabIndex = 3;
            this.grpSettings.TabStop = false;
            this.grpSettings.Text = "Settings";
            //
            // lblParallelism
            //
            this.lblParallelism.AutoSize = true;
            this.lblParallelism.Location = new System.Drawing.Point(145, 22);
            this.lblParallelism.Name = "lblParallelism";
            this.lblParallelism.Size = new System.Drawing.Size(114, 13);
            this.lblParallelism.TabIndex = 0;
            this.lblParallelism.Text = "Degree of Parallelism:";
            //
            // nudParallelism
            //
            this.nudParallelism.Location = new System.Drawing.Point(265, 20);
            this.nudParallelism.Maximum = new decimal(new int[] { 100, 0, 0, 0 });
            this.nudParallelism.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this.nudParallelism.Name = "nudParallelism";
            this.nudParallelism.Size = new System.Drawing.Size(60, 20);
            this.nudParallelism.TabIndex = 1;
            this.nudParallelism.Value = new decimal(new int[] { 4, 0, 0, 0 });
            //
            // lblRetries
            //
            this.lblRetries.AutoSize = true;
            this.lblRetries.Location = new System.Drawing.Point(345, 22);
            this.lblRetries.Name = "lblRetries";
            this.lblRetries.Size = new System.Drawing.Size(65, 13);
            this.lblRetries.TabIndex = 2;
            this.lblRetries.Text = "Max retries:";
            //
            // nudRetries
            //
            this.nudRetries.Location = new System.Drawing.Point(415, 20);
            this.nudRetries.Maximum = new decimal(new int[] { 50, 0, 0, 0 });
            this.nudRetries.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this.nudRetries.Name = "nudRetries";
            this.nudRetries.Size = new System.Drawing.Size(50, 20);
            this.nudRetries.TabIndex = 3;
            this.nudRetries.Value = new decimal(new int[] { 10, 0, 0, 0 });
            //
            // chkBypassPlugins
            //
            this.chkBypassPlugins.AutoSize = true;
            this.chkBypassPlugins.Checked = true;
            this.chkBypassPlugins.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkBypassPlugins.Location = new System.Drawing.Point(480, 22);
            this.chkBypassPlugins.Name = "chkBypassPlugins";
            this.chkBypassPlugins.Size = new System.Drawing.Size(170, 17);
            this.chkBypassPlugins.TabIndex = 4;
            this.chkBypassPlugins.Text = "Bypass plugins/workflows";
            this.chkBypassPlugins.UseVisualStyleBackColor = true;
            //
            // chkVerboseLog
            //
            this.chkVerboseLog.AutoSize = true;
            this.chkVerboseLog.Checked = true;
            this.chkVerboseLog.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkVerboseLog.Location = new System.Drawing.Point(670, 22);
            this.chkVerboseLog.Name = "chkVerboseLog";
            this.chkVerboseLog.Size = new System.Drawing.Size(100, 17);
            this.chkVerboseLog.TabIndex = 5;
            this.chkVerboseLog.Text = "Log every pair";
            this.chkVerboseLog.UseVisualStyleBackColor = true;
            //
            // lblBatchSize
            //
            this.lblBatchSize.AutoSize = true;
            this.lblBatchSize.Location = new System.Drawing.Point(10, 22);
            this.lblBatchSize.Name = "lblBatchSize";
            this.lblBatchSize.Size = new System.Drawing.Size(61, 13);
            this.lblBatchSize.TabIndex = 6;
            this.lblBatchSize.Text = "Batch Size:";
            //
            // nudBatchSize
            //
            this.nudBatchSize.Location = new System.Drawing.Point(77, 20);
            this.nudBatchSize.Maximum = new decimal(new int[] { 1000, 0, 0, 0 });
            this.nudBatchSize.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this.nudBatchSize.Name = "nudBatchSize";
            this.nudBatchSize.Size = new System.Drawing.Size(55, 20);
            this.nudBatchSize.TabIndex = 7;
            this.nudBatchSize.Value = new decimal(new int[] { 1, 0, 0, 0 });
            //
            // chkFireAndForget
            //
            this.chkFireAndForget.AutoSize = true;
            this.chkFireAndForget.Checked = true;
            this.chkFireAndForget.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkFireAndForget.Location = new System.Drawing.Point(10, 46);
            this.chkFireAndForget.Name = "chkFireAndForget";
            this.chkFireAndForget.Size = new System.Drawing.Size(155, 17);
            this.chkFireAndForget.TabIndex = 8;
            this.chkFireAndForget.Text = "Skip per-item responses";
            this.chkFireAndForget.UseVisualStyleBackColor = true;
            this.toolTip.SetToolTip(this.chkFireAndForget, "Skips per-item response tracking for faster server processing.\nBatches assume success unless the entire call fails (transport error).\nOnly applies when Batch Size > 1.");
            //
            // grpProgress
            //
            this.grpProgress.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.grpProgress.Controls.Add(this.lblErrors);
            this.grpProgress.Controls.Add(this.lblDuplicates);
            this.grpProgress.Controls.Add(this.lblProgress);
            this.grpProgress.Controls.Add(this.progressBar);
            this.grpProgress.Location = new System.Drawing.Point(12, 422);
            this.grpProgress.Name = "grpProgress";
            this.grpProgress.Size = new System.Drawing.Size(776, 68);
            this.grpProgress.TabIndex = 4;
            this.grpProgress.TabStop = false;
            this.grpProgress.Text = "Progress";
            //
            // progressBar
            //
            this.progressBar.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.progressBar.Location = new System.Drawing.Point(10, 20);
            this.progressBar.Name = "progressBar";
            this.progressBar.Size = new System.Drawing.Size(756, 20);
            this.progressBar.TabIndex = 0;
            //
            // lblProgress
            //
            this.lblProgress.AutoSize = true;
            this.lblProgress.Location = new System.Drawing.Point(10, 46);
            this.lblProgress.Name = "lblProgress";
            this.lblProgress.Size = new System.Drawing.Size(74, 13);
            this.lblProgress.TabIndex = 1;
            this.lblProgress.Text = "0 / 0 completed";
            //
            // lblDuplicates
            //
            this.lblDuplicates.AutoSize = true;
            this.lblDuplicates.Location = new System.Drawing.Point(250, 46);
            this.lblDuplicates.Name = "lblDuplicates";
            this.lblDuplicates.Size = new System.Drawing.Size(99, 13);
            this.lblDuplicates.TabIndex = 2;
            this.lblDuplicates.Text = "0 duplicates skipped";
            //
            // lblErrors
            //
            this.lblErrors.AutoSize = true;
            this.lblErrors.Location = new System.Drawing.Point(500, 46);
            this.lblErrors.Name = "lblErrors";
            this.lblErrors.Size = new System.Drawing.Size(40, 13);
            this.lblErrors.TabIndex = 3;
            this.lblErrors.Text = "0 errors";
            //
            // btnStart
            //
            this.btnStart.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnStart.Enabled = false;
            this.btnStart.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold);
            this.btnStart.Location = new System.Drawing.Point(12, 496);
            this.btnStart.Name = "btnStart";
            this.btnStart.Size = new System.Drawing.Size(100, 30);
            this.btnStart.TabIndex = 5;
            this.btnStart.Text = "Associate";
            this.btnStart.UseVisualStyleBackColor = true;
            this.btnStart.Click += new System.EventHandler(this.btnStart_Click);
            //
            // btnStop
            //
            this.btnStop.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnStop.Enabled = false;
            this.btnStop.Location = new System.Drawing.Point(118, 496);
            this.btnStop.Name = "btnStop";
            this.btnStop.Size = new System.Drawing.Size(100, 30);
            this.btnStop.TabIndex = 6;
            this.btnStop.Text = "Stop";
            this.btnStop.UseVisualStyleBackColor = true;
            this.btnStop.Click += new System.EventHandler(this.btnStop_Click);
            //
            // txtLog
            //
            this.txtLog.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.txtLog.Font = new System.Drawing.Font("Consolas", 8.25F);
            this.txtLog.Location = new System.Drawing.Point(12, 532);
            this.txtLog.Multiline = true;
            this.txtLog.Name = "txtLog";
            this.txtLog.ReadOnly = true;
            this.txtLog.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtLog.Size = new System.Drawing.Size(776, 100);
            this.txtLog.TabIndex = 7;
            //
            // SpeedyNtoNAssociateControl
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.txtLog);
            this.Controls.Add(this.btnStop);
            this.Controls.Add(this.btnStart);
            this.Controls.Add(this.grpProgress);
            this.Controls.Add(this.grpSettings);
            this.Controls.Add(this.tabDataSource);
            this.Controls.Add(this.grpRelationship);
            this.Controls.Add(this.lblStatus);
            this.Name = "SpeedyNtoNAssociateControl";
            this.Size = new System.Drawing.Size(800, 642);
            this.Load += new System.EventHandler(this.SpeedyNtoNAssociateControl_Load);
            this.grpRelationship.ResumeLayout(false);
            this.grpRelationship.PerformLayout();
            this.tabDataSource.ResumeLayout(false);
            this.tabCsv.ResumeLayout(false);
            this.tabCsv.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvCsvPreview)).EndInit();
            this.tabFetchXml.ResumeLayout(false);
            this.tabFetchXml.PerformLayout();
            this.splitFetch.Panel1.ResumeLayout(false);
            this.splitFetch.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitFetch)).EndInit();
            this.splitFetch.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dgvFetchPreview)).EndInit();
            this.tabSql.ResumeLayout(false);
            this.tabSql.PerformLayout();
            this.splitSql.Panel1.ResumeLayout(false);
            this.splitSql.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitSql)).EndInit();
            this.splitSql.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dgvSqlPreview)).EndInit();
            this.grpSettings.ResumeLayout(false);
            this.grpSettings.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.nudBatchSize)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudRetries)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudParallelism)).EndInit();
            this.grpProgress.ResumeLayout(false);
            this.grpProgress.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.GroupBox grpRelationship;
        private System.Windows.Forms.ComboBox cmbRelationship;
        private System.Windows.Forms.Label lblRelationship;
        private System.Windows.Forms.ComboBox cmbEntity2;
        private System.Windows.Forms.Label lblEntity2;
        private System.Windows.Forms.ComboBox cmbEntity1;
        private System.Windows.Forms.Label lblEntity1;
        private System.Windows.Forms.Button btnLoadEntities;
        private System.Windows.Forms.TabControl tabDataSource;
        private System.Windows.Forms.TabPage tabCsv;
        private System.Windows.Forms.Label lblCsvCount;
        private System.Windows.Forms.DataGridView dgvCsvPreview;
        private System.Windows.Forms.DataGridViewTextBoxColumn colGuid1;
        private System.Windows.Forms.DataGridViewTextBoxColumn colGuid2;
        private System.Windows.Forms.Button btnBrowseCsv;
        private System.Windows.Forms.TextBox txtCsvPath;
        private System.Windows.Forms.TabPage tabFetchXml;
        private System.Windows.Forms.Label lblFetchInstructions;
        private System.Windows.Forms.RichTextBox txtFetchXml;
        private System.Windows.Forms.Label lblFetchXmlCount;
        private System.Windows.Forms.Button btnPreviewFetchXml;
        private System.Windows.Forms.Button btnFormatXml;
        private System.Windows.Forms.SplitContainer splitFetch;
        private System.Windows.Forms.DataGridView dgvFetchPreview;
        private System.Windows.Forms.DataGridViewTextBoxColumn colFetchGuid1;
        private System.Windows.Forms.DataGridViewTextBoxColumn colFetchGuid2;
        private System.Windows.Forms.TabPage tabSql;
        private System.Windows.Forms.Label lblSqlInstructions;
        private System.Windows.Forms.SplitContainer splitSql;
        private System.Windows.Forms.RichTextBox txtSqlQuery;
        private System.Windows.Forms.DataGridView dgvSqlPreview;
        private System.Windows.Forms.DataGridViewTextBoxColumn colSqlGuid1;
        private System.Windows.Forms.DataGridViewTextBoxColumn colSqlGuid2;
        private System.Windows.Forms.Button btnPreviewSql;
        private System.Windows.Forms.Label lblSqlCount;
        private System.Windows.Forms.GroupBox grpSettings;
        private System.Windows.Forms.CheckBox chkBypassPlugins;
        private System.Windows.Forms.CheckBox chkVerboseLog;
        private System.Windows.Forms.NumericUpDown nudRetries;
        private System.Windows.Forms.Label lblRetries;
        private System.Windows.Forms.NumericUpDown nudParallelism;
        private System.Windows.Forms.Label lblParallelism;
        private System.Windows.Forms.GroupBox grpProgress;
        private System.Windows.Forms.Label lblErrors;
        private System.Windows.Forms.Label lblDuplicates;
        private System.Windows.Forms.Label lblProgress;
        private System.Windows.Forms.ProgressBar progressBar;
        private System.Windows.Forms.Button btnStart;
        private System.Windows.Forms.Button btnStop;
        private System.Windows.Forms.TextBox txtLog;
        private System.Windows.Forms.NumericUpDown nudBatchSize;
        private System.Windows.Forms.Label lblBatchSize;
        private System.Windows.Forms.CheckBox chkFireAndForget;
        private System.Windows.Forms.ToolTip toolTip;
    }
}
