namespace Deephaven.ExcelAddIn.Gui {

  partial class StatusMonitorDialog {
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
      dataGridView1 = new DataGridView();
      statusLabel = new Label();
      versionLabel = new Label();
      retryButton = new Button();
      ((System.ComponentModel.ISupportInitialize)dataGridView1).BeginInit();
      SuspendLayout();
      // 
      // dataGridView1
      // 
      dataGridView1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
      dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
      dataGridView1.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
      dataGridView1.Location = new Point(39, 73);
      dataGridView1.Name = "dataGridView1";
      dataGridView1.ReadOnly = true;
      dataGridView1.RowHeadersWidth = 62;
      dataGridView1.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
      dataGridView1.Size = new Size(996, 513);
      dataGridView1.TabIndex = 0;
      dataGridView1.SelectionChanged += dataGridView1_SelectionChanged;
      // 
      // statusLabel
      // 
      statusLabel.AutoSize = true;
      statusLabel.Font = new Font("Segoe UI", 12F, FontStyle.Regular, GraphicsUnit.Point, 0);
      statusLabel.Location = new Point(39, 18);
      statusLabel.Name = "statusLabel";
      statusLabel.Size = new Size(78, 32);
      statusLabel.TabIndex = 1;
      statusLabel.Text = "Status";
      // 
      // versionLabel
      // 
      versionLabel.AutoSize = true;
      versionLabel.Location = new Point(12, 627);
      versionLabel.Name = "versionLabel";
      versionLabel.Size = new Size(79, 25);
      versionLabel.TabIndex = 7;
      versionLabel.Text = "(version)";
      // 
      // retryButton
      // 
      retryButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
      retryButton.Location = new Point(907, 605);
      retryButton.Name = "retryButton";
      retryButton.Size = new Size(128, 34);
      retryButton.TabIndex = 8;
      retryButton.Text = "Retry";
      retryButton.UseVisualStyleBackColor = true;
      retryButton.Click += retryButton_Click;
      // 
      // StatusMonitorDialog
      // 
      AutoScaleDimensions = new SizeF(10F, 25F);
      AutoScaleMode = AutoScaleMode.Font;
      ClientSize = new Size(1086, 661);
      Controls.Add(retryButton);
      Controls.Add(versionLabel);
      Controls.Add(statusLabel);
      Controls.Add(dataGridView1);
      Name = "StatusMonitorDialog";
      Text = "StatusMonitorDialog";
      ((System.ComponentModel.ISupportInitialize)dataGridView1).EndInit();
      ResumeLayout(false);
      PerformLayout();
    }

    #endregion

    private DataGridView dataGridView1;
    private Label statusLabel;
    private Label versionLabel;
    private Button retryButton;
  }
}