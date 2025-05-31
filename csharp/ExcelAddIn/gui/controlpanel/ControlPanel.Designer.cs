namespace Deephaven.ExcelAddIn.Gui {
  partial class ControlPanel {
    /// <summary>
    /// Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    /// Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing) {
      if (disposing && (components != null)) {
        components.Dispose();
      }
      base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>
    /// Required method for Designer support - do not modify
    /// the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent() {
      splitContainer1 = new SplitContainer();
      makeDefaultButton = new Button();
      reconnectButton = new Button();
      deleteButton = new Button();
      editButton = new Button();
      connectionsLabel = new Label();
      newButton = new Button();
      endpointDataGrid = new DataGridView();
      retryButton = new Button();
      versionLabel = new Label();
      statusLabel = new Label();
      statusDataGrid = new DataGridView();
      ((System.ComponentModel.ISupportInitialize)splitContainer1).BeginInit();
      splitContainer1.Panel1.SuspendLayout();
      splitContainer1.Panel2.SuspendLayout();
      splitContainer1.SuspendLayout();
      ((System.ComponentModel.ISupportInitialize)endpointDataGrid).BeginInit();
      ((System.ComponentModel.ISupportInitialize)statusDataGrid).BeginInit();
      SuspendLayout();
      // 
      // splitContainer1
      // 
      splitContainer1.Dock = DockStyle.Fill;
      splitContainer1.Location = new Point(0, 0);
      splitContainer1.Name = "splitContainer1";
      splitContainer1.Orientation = Orientation.Horizontal;
      // 
      // splitContainer1.Panel1
      // 
      splitContainer1.Panel1.Controls.Add(makeDefaultButton);
      splitContainer1.Panel1.Controls.Add(reconnectButton);
      splitContainer1.Panel1.Controls.Add(deleteButton);
      splitContainer1.Panel1.Controls.Add(editButton);
      splitContainer1.Panel1.Controls.Add(connectionsLabel);
      splitContainer1.Panel1.Controls.Add(newButton);
      splitContainer1.Panel1.Controls.Add(endpointDataGrid);
      // 
      // splitContainer1.Panel2
      // 
      splitContainer1.Panel2.Controls.Add(retryButton);
      splitContainer1.Panel2.Controls.Add(versionLabel);
      splitContainer1.Panel2.Controls.Add(statusLabel);
      splitContainer1.Panel2.Controls.Add(statusDataGrid);
      splitContainer1.Size = new Size(1221, 1282);
      splitContainer1.SplitterDistance = 620;
      splitContainer1.TabIndex = 0;
      // 
      // makeDefaultButton
      // 
      makeDefaultButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
      makeDefaultButton.Location = new Point(554, 579);
      makeDefaultButton.Name = "makeDefaultButton";
      makeDefaultButton.Size = new Size(139, 34);
      makeDefaultButton.TabIndex = 9;
      makeDefaultButton.Text = "Make Default";
      makeDefaultButton.UseVisualStyleBackColor = true;
      // 
      // reconnectButton
      // 
      reconnectButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
      reconnectButton.Location = new Point(717, 579);
      reconnectButton.Name = "reconnectButton";
      reconnectButton.Size = new Size(112, 34);
      reconnectButton.TabIndex = 11;
      reconnectButton.Text = "Reconnect";
      reconnectButton.UseVisualStyleBackColor = true;
      // 
      // deleteButton
      // 
      deleteButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
      deleteButton.Location = new Point(420, 579);
      deleteButton.Name = "deleteButton";
      deleteButton.Size = new Size(112, 34);
      deleteButton.TabIndex = 8;
      deleteButton.Text = "Delete";
      deleteButton.UseVisualStyleBackColor = true;
      // 
      // editButton
      // 
      editButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
      editButton.Location = new Point(857, 579);
      editButton.Name = "editButton";
      editButton.Size = new Size(112, 34);
      editButton.TabIndex = 12;
      editButton.Text = "Edit...";
      editButton.UseVisualStyleBackColor = true;
      // 
      // connectionsLabel
      // 
      connectionsLabel.AutoSize = true;
      connectionsLabel.Font = new Font("Segoe UI", 12F, FontStyle.Regular, GraphicsUnit.Point, 0);
      connectionsLabel.Location = new Point(122, 8);
      connectionsLabel.Name = "connectionsLabel";
      connectionsLabel.Size = new Size(147, 32);
      connectionsLabel.TabIndex = 10;
      connectionsLabel.Text = "Connections";
      // 
      // newButton
      // 
      newButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
      newButton.Location = new Point(1000, 579);
      newButton.Name = "newButton";
      newButton.Size = new Size(128, 34);
      newButton.TabIndex = 13;
      newButton.Text = "New...";
      newButton.UseVisualStyleBackColor = true;
      // 
      // endpointDataGrid
      // 
      endpointDataGrid.AllowUserToAddRows = false;
      endpointDataGrid.AllowUserToDeleteRows = false;
      endpointDataGrid.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
      endpointDataGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
      endpointDataGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
      endpointDataGrid.Location = new Point(141, 55);
      endpointDataGrid.Name = "endpointDataGrid";
      endpointDataGrid.ReadOnly = true;
      endpointDataGrid.RowHeadersWidth = 62;
      endpointDataGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
      endpointDataGrid.Size = new Size(1006, 498);
      endpointDataGrid.TabIndex = 7;
      // 
      // retryButton
      // 
      retryButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
      retryButton.Location = new Point(994, 599);
      retryButton.Name = "retryButton";
      retryButton.Size = new Size(128, 34);
      retryButton.TabIndex = 12;
      retryButton.Text = "Retry";
      retryButton.UseVisualStyleBackColor = true;
      // 
      // versionLabel
      // 
      versionLabel.AutoSize = true;
      versionLabel.Location = new Point(99, 621);
      versionLabel.Name = "versionLabel";
      versionLabel.Size = new Size(79, 25);
      versionLabel.TabIndex = 11;
      versionLabel.Text = "(version)";
      // 
      // statusLabel
      // 
      statusLabel.AutoSize = true;
      statusLabel.Font = new Font("Segoe UI", 12F, FontStyle.Regular, GraphicsUnit.Point, 0);
      statusLabel.Location = new Point(126, 12);
      statusLabel.Name = "statusLabel";
      statusLabel.Size = new Size(78, 32);
      statusLabel.TabIndex = 10;
      statusLabel.Text = "Status";
      // 
      // statusDataGrid
      // 
      statusDataGrid.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
      statusDataGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
      statusDataGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
      statusDataGrid.Location = new Point(132, 80);
      statusDataGrid.Name = "statusDataGrid";
      statusDataGrid.ReadOnly = true;
      statusDataGrid.RowHeadersWidth = 62;
      statusDataGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
      statusDataGrid.Size = new Size(996, 513);
      statusDataGrid.TabIndex = 9;
      // 
      // ControlPanel
      // 
      AutoScaleDimensions = new SizeF(10F, 25F);
      AutoScaleMode = AutoScaleMode.Font;
      ClientSize = new Size(1221, 1282);
      Controls.Add(splitContainer1);
      Name = "ControlPanel";
      Text = "ControlPanel";
      splitContainer1.Panel1.ResumeLayout(false);
      splitContainer1.Panel1.PerformLayout();
      splitContainer1.Panel2.ResumeLayout(false);
      splitContainer1.Panel2.PerformLayout();
      ((System.ComponentModel.ISupportInitialize)splitContainer1).EndInit();
      splitContainer1.ResumeLayout(false);
      ((System.ComponentModel.ISupportInitialize)endpointDataGrid).EndInit();
      ((System.ComponentModel.ISupportInitialize)statusDataGrid).EndInit();
      ResumeLayout(false);
    }

    #endregion

    private SplitContainer splitContainer1;
    private Label versionLabel;
    private Label statusLabel;
    private Label connectionsLabel;
    public DataGridView endpointDataGrid;
    public Button deleteButton;
    public Button reconnectButton;
    public Button makeDefaultButton;
    public Button editButton;
    public Button retryButton;
    public DataGridView statusDataGrid;
    public Button newButton;
  }
}