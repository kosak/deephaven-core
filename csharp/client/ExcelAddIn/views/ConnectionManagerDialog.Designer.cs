namespace ExcelAddIn.views {
  partial class ConnectionManagerDialog {
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
      colorDialog1 = new ColorDialog();
      dataGridView1 = new DataGridView();
      ((System.ComponentModel.ISupportInitialize)dataGridView1).BeginInit();
      SuspendLayout();
      // 
      // dataGridView1
      // 
      dataGridView1.AllowUserToAddRows = false;
      dataGridView1.AllowUserToDeleteRows = false;
      dataGridView1.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
      dataGridView1.Location = new Point(186, 105);
      dataGridView1.Name = "dataGridView1";
      dataGridView1.ReadOnly = true;
      dataGridView1.RowHeadersWidth = 62;
      dataGridView1.Size = new Size(979, 454);
      dataGridView1.TabIndex = 0;
      // 
      // ConnectionManager
      // 
      AutoScaleDimensions = new SizeF(10F, 25F);
      AutoScaleMode = AutoScaleMode.Font;
      ClientSize = new Size(2054, 967);
      Controls.Add(dataGridView1);
      Name = "ConnectionManagerDialog";
      Text = "ConnectionManager";
      ((System.ComponentModel.ISupportInitialize)dataGridView1).EndInit();
      ResumeLayout(false);
    }

    #endregion

    private ColorDialog colorDialog1;
    private DataGridView dataGridView1;
  }
}