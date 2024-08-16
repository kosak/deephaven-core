namespace ExcelAddIn.views {
  partial class CredentialsDialog {
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
      flowLayoutPanel1 = new FlowLayoutPanel();
      corePlusPanel = new Panel();
      textBox2 = new TextBox();
      label3 = new Label();
      corePanel = new Panel();
      textBox3 = new TextBox();
      label2 = new Label();
      isCorePlusRadioButton = new RadioButton();
      isCoreRadioButton = new RadioButton();
      button1 = new Button();
      textBox1 = new TextBox();
      label1 = new Label();
      connectionTypeGroup = new GroupBox();
      flowLayoutPanel1.SuspendLayout();
      corePlusPanel.SuspendLayout();
      corePanel.SuspendLayout();
      connectionTypeGroup.SuspendLayout();
      SuspendLayout();
      // 
      // flowLayoutPanel1
      // 
      flowLayoutPanel1.Controls.Add(corePlusPanel);
      flowLayoutPanel1.Controls.Add(corePanel);
      flowLayoutPanel1.Location = new Point(58, 209);
      flowLayoutPanel1.Name = "flowLayoutPanel1";
      flowLayoutPanel1.Size = new Size(851, 403);
      flowLayoutPanel1.TabIndex = 0;
      // 
      // corePlusPanel
      // 
      corePlusPanel.Controls.Add(textBox2);
      corePlusPanel.Controls.Add(label3);
      corePlusPanel.Location = new Point(3, 3);
      corePlusPanel.Name = "corePlusPanel";
      corePlusPanel.Size = new Size(774, 242);
      corePlusPanel.TabIndex = 0;
      // 
      // textBox2
      // 
      textBox2.Location = new Point(213, 36);
      textBox2.Name = "textBox2";
      textBox2.Size = new Size(444, 31);
      textBox2.TabIndex = 1;
      // 
      // label3
      // 
      label3.AutoSize = true;
      label3.Location = new Point(35, 39);
      label3.Name = "label3";
      label3.Size = new Size(91, 25);
      label3.TabIndex = 0;
      label3.Text = "JSON URL";
      // 
      // corePanel
      // 
      corePanel.Controls.Add(textBox3);
      corePanel.Controls.Add(label2);
      corePanel.Location = new Point(3, 251);
      corePanel.Name = "corePanel";
      corePanel.Size = new Size(585, 150);
      corePanel.TabIndex = 1;
      // 
      // textBox3
      // 
      textBox3.Location = new Point(210, 20);
      textBox3.Name = "textBox3";
      textBox3.Size = new Size(323, 31);
      textBox3.TabIndex = 1;
      // 
      // label2
      // 
      label2.AutoSize = true;
      label2.Location = new Point(19, 26);
      label2.Name = "label2";
      label2.Size = new Size(153, 25);
      label2.TabIndex = 0;
      label2.Text = "Connection String";
      label2.Click += label2_Click;
      // 
      // isCorePlusRadioButton
      // 
      isCorePlusRadioButton.AutoSize = true;
      isCorePlusRadioButton.Location = new Point(6, 39);
      isCorePlusRadioButton.Name = "isCorePlusRadioButton";
      isCorePlusRadioButton.Size = new Size(180, 29);
      isCorePlusRadioButton.TabIndex = 1;
      isCorePlusRadioButton.TabStop = true;
      isCorePlusRadioButton.Text = "Deephaven Core+";
      isCorePlusRadioButton.UseVisualStyleBackColor = true;
      isCorePlusRadioButton.CheckedChanged += radioButton1_CheckedChanged;
      // 
      // isCoreRadioButton
      // 
      isCoreRadioButton.AutoSize = true;
      isCoreRadioButton.Location = new Point(308, 39);
      isCoreRadioButton.Name = "isCoreRadioButton";
      isCoreRadioButton.Size = new Size(168, 29);
      isCoreRadioButton.TabIndex = 2;
      isCoreRadioButton.TabStop = true;
      isCoreRadioButton.Text = "Deephaven Core";
      isCoreRadioButton.UseVisualStyleBackColor = true;
      isCoreRadioButton.CheckedChanged += radioButton2_CheckedChanged;
      // 
      // button1
      // 
      button1.Location = new Point(709, 630);
      button1.Name = "button1";
      button1.Size = new Size(200, 34);
      button1.TabIndex = 3;
      button1.Text = "Add Credentials";
      button1.UseVisualStyleBackColor = true;
      // 
      // textBox1
      // 
      textBox1.Location = new Point(201, 35);
      textBox1.Name = "textBox1";
      textBox1.Size = new Size(393, 31);
      textBox1.TabIndex = 4;
      // 
      // label1
      // 
      label1.AutoSize = true;
      label1.Location = new Point(50, 43);
      label1.Name = "label1";
      label1.Size = new Size(125, 25);
      label1.TabIndex = 5;
      label1.Text = "Connection ID";
      // 
      // connectionTypeGroup
      // 
      connectionTypeGroup.Controls.Add(isCorePlusRadioButton);
      connectionTypeGroup.Controls.Add(isCoreRadioButton);
      connectionTypeGroup.Location = new Point(58, 123);
      connectionTypeGroup.Name = "connectionTypeGroup";
      connectionTypeGroup.Size = new Size(588, 80);
      connectionTypeGroup.TabIndex = 8;
      connectionTypeGroup.TabStop = false;
      connectionTypeGroup.Text = "Connection Type";
      // 
      // CredentialsDialog
      // 
      AutoScaleDimensions = new SizeF(10F, 25F);
      AutoScaleMode = AutoScaleMode.Font;
      ClientSize = new Size(1243, 732);
      Controls.Add(connectionTypeGroup);
      Controls.Add(label1);
      Controls.Add(textBox1);
      Controls.Add(button1);
      Controls.Add(flowLayoutPanel1);
      Name = "CredentialsDialog";
      Text = "CredentialsDialog";
      flowLayoutPanel1.ResumeLayout(false);
      corePlusPanel.ResumeLayout(false);
      corePlusPanel.PerformLayout();
      corePanel.ResumeLayout(false);
      corePanel.PerformLayout();
      connectionTypeGroup.ResumeLayout(false);
      connectionTypeGroup.PerformLayout();
      ResumeLayout(false);
      PerformLayout();
    }

    #endregion

    private FlowLayoutPanel flowLayoutPanel1;
    private RadioButton isCorePlusRadioButton;
    private RadioButton isCoreRadioButton;
    private Button button1;
    private TextBox textBox1;
    private Label label1;
    private GroupBox connectionTypeGroup;
    private Panel corePlusPanel;
    private Panel corePanel;
    private Label label3;
    private Label label2;
    private TextBox textBox2;
    private TextBox textBox3;
  }
}