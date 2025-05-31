namespace ExcelAddIn.gui.controlpanel {
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
      retryButton = new Button();
      versionLabel = new Label();
      statusLabel = new Label();
      dataGridView1 = new DataGridView();
      label1 = new Label();
      makeDefaultButton = new Button();
      reconnectButton = new Button();
      deleteButton = new Button();
      editButton = new Button();
      connectionsLabel = new Label();
      newButton = new Button();
      dataGridView2 = new DataGridView();
      ((System.ComponentModel.ISupportInitialize)splitContainer1).BeginInit();
      splitContainer1.Panel1.SuspendLayout();
      splitContainer1.Panel2.SuspendLayout();
      splitContainer1.SuspendLayout();
      ((System.ComponentModel.ISupportInitialize)dataGridView1).BeginInit();
      ((System.ComponentModel.ISupportInitialize)dataGridView2).BeginInit();
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
      splitContainer1.Panel1.Controls.Add(label1);
      splitContainer1.Panel1.Controls.Add(makeDefaultButton);
      splitContainer1.Panel1.Controls.Add(reconnectButton);
      splitContainer1.Panel1.Controls.Add(deleteButton);
      splitContainer1.Panel1.Controls.Add(editButton);
      splitContainer1.Panel1.Controls.Add(connectionsLabel);
      splitContainer1.Panel1.Controls.Add(newButton);
      splitContainer1.Panel1.Controls.Add(dataGridView2);
      // 
      // splitContainer1.Panel2
      // 
      splitContainer1.Panel2.Controls.Add(retryButton);
      splitContainer1.Panel2.Controls.Add(versionLabel);
      splitContainer1.Panel2.Controls.Add(statusLabel);
      splitContainer1.Panel2.Controls.Add(dataGridView1);
      splitContainer1.Size = new Size(1221, 1282);
      splitContainer1.SplitterDistance = 620;
      splitContainer1.TabIndex = 0;
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
      // dataGridView1
      // 
      dataGridView1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
      dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
      dataGridView1.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
      dataGridView1.Location = new Point(126, 67);
      dataGridView1.Name = "dataGridView1";
      dataGridView1.ReadOnly = true;
      dataGridView1.RowHeadersWidth = 62;
      dataGridView1.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
      dataGridView1.Size = new Size(996, 513);
      dataGridView1.TabIndex = 9;
      // 
      // label1
      // 
      label1.AutoSize = true;
      label1.Location = new Point(93, 556);
      label1.Name = "label1";
      label1.Size = new Size(79, 25);
      label1.TabIndex = 14;
      label1.Text = "(version)";
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
      // dataGridView2
      // 
      dataGridView2.AllowUserToAddRows = false;
      dataGridView2.AllowUserToDeleteRows = false;
      dataGridView2.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
      dataGridView2.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
      dataGridView2.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
      dataGridView2.Location = new Point(122, 58);
      dataGridView2.Name = "dataGridView2";
      dataGridView2.ReadOnly = true;
      dataGridView2.RowHeadersWidth = 62;
      dataGridView2.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
      dataGridView2.Size = new Size(1006, 498);
      dataGridView2.TabIndex = 7;
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
      ((System.ComponentModel.ISupportInitialize)dataGridView1).EndInit();
      ((System.ComponentModel.ISupportInitialize)dataGridView2).EndInit();
      ResumeLayout(false);
    }

    #endregion

    private SplitContainer splitContainer1;
    private Button retryButton;
    private Label versionLabel;
    private Label statusLabel;
    private DataGridView dataGridView1;
    private Label label1;
    private Button makeDefaultButton;
    private Button reconnectButton;
    private Button deleteButton;
    private Button editButton;
    private Label connectionsLabel;
    private Button newButton;
    private DataGridView dataGridView2;
  }
}