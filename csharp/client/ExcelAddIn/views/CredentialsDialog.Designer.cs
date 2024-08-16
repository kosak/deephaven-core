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
      radioButton1 = new RadioButton();
      radioButton2 = new RadioButton();
      button1 = new Button();
      textBox1 = new TextBox();
      label1 = new Label();
      groupBox1 = new GroupBox();
      panel1 = new Panel();
      panel2 = new Panel();
      label2 = new Label();
      label3 = new Label();
      textBox2 = new TextBox();
      textBox3 = new TextBox();
      flowLayoutPanel1.SuspendLayout();
      groupBox1.SuspendLayout();
      panel1.SuspendLayout();
      panel2.SuspendLayout();
      SuspendLayout();
      // 
      // flowLayoutPanel1
      // 
      flowLayoutPanel1.Controls.Add(panel1);
      flowLayoutPanel1.Controls.Add(panel2);
      flowLayoutPanel1.Location = new Point(58, 209);
      flowLayoutPanel1.Name = "flowLayoutPanel1";
      flowLayoutPanel1.Size = new Size(851, 403);
      flowLayoutPanel1.TabIndex = 0;
      // 
      // radioButton1
      // 
      radioButton1.AutoSize = true;
      radioButton1.Location = new Point(6, 39);
      radioButton1.Name = "radioButton1";
      radioButton1.Size = new Size(180, 29);
      radioButton1.TabIndex = 1;
      radioButton1.TabStop = true;
      radioButton1.Text = "Deephaven Core+";
      radioButton1.UseVisualStyleBackColor = true;
      radioButton1.CheckedChanged += radioButton1_CheckedChanged;
      // 
      // radioButton2
      // 
      radioButton2.AutoSize = true;
      radioButton2.Location = new Point(308, 39);
      radioButton2.Name = "radioButton2";
      radioButton2.Size = new Size(168, 29);
      radioButton2.TabIndex = 2;
      radioButton2.TabStop = true;
      radioButton2.Text = "Deephaven Core";
      radioButton2.UseVisualStyleBackColor = true;
      radioButton2.CheckedChanged += radioButton2_CheckedChanged;
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
      // groupBox1
      // 
      groupBox1.Controls.Add(radioButton1);
      groupBox1.Controls.Add(radioButton2);
      groupBox1.Location = new Point(58, 123);
      groupBox1.Name = "groupBox1";
      groupBox1.Size = new Size(588, 80);
      groupBox1.TabIndex = 8;
      groupBox1.TabStop = false;
      groupBox1.Text = "Connection Type";
      // 
      // panel1
      // 
      panel1.Controls.Add(textBox2);
      panel1.Controls.Add(label3);
      panel1.Location = new Point(3, 3);
      panel1.Name = "panel1";
      panel1.Size = new Size(774, 242);
      panel1.TabIndex = 0;
      // 
      // panel2
      // 
      panel2.Controls.Add(textBox3);
      panel2.Controls.Add(label2);
      panel2.Location = new Point(3, 251);
      panel2.Name = "panel2";
      panel2.Size = new Size(585, 150);
      panel2.TabIndex = 1;
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
      // label3
      // 
      label3.AutoSize = true;
      label3.Location = new Point(35, 39);
      label3.Name = "label3";
      label3.Size = new Size(91, 25);
      label3.TabIndex = 0;
      label3.Text = "JSON URL";
      // 
      // textBox2
      // 
      textBox2.Location = new Point(213, 36);
      textBox2.Name = "textBox2";
      textBox2.Size = new Size(444, 31);
      textBox2.TabIndex = 1;
      // 
      // textBox3
      // 
      textBox3.Location = new Point(210, 20);
      textBox3.Name = "textBox3";
      textBox3.Size = new Size(323, 31);
      textBox3.TabIndex = 1;
      // 
      // CredentialsDialog
      // 
      AutoScaleDimensions = new SizeF(10F, 25F);
      AutoScaleMode = AutoScaleMode.Font;
      ClientSize = new Size(1243, 732);
      Controls.Add(groupBox1);
      Controls.Add(label1);
      Controls.Add(textBox1);
      Controls.Add(button1);
      Controls.Add(flowLayoutPanel1);
      Name = "CredentialsDialog";
      Text = "CredentialsDialog";
      flowLayoutPanel1.ResumeLayout(false);
      groupBox1.ResumeLayout(false);
      groupBox1.PerformLayout();
      panel1.ResumeLayout(false);
      panel1.PerformLayout();
      panel2.ResumeLayout(false);
      panel2.PerformLayout();
      ResumeLayout(false);
      PerformLayout();
    }

    #endregion

    private FlowLayoutPanel flowLayoutPanel1;
    private RadioButton radioButton1;
    private RadioButton radioButton2;
    private Button button1;
    private TextBox textBox1;
    private Label label1;
    private GroupBox groupBox1;
    private Panel panel1;
    private Panel panel2;
    private Label label3;
    private Label label2;
    private TextBox textBox2;
    private TextBox textBox3;
  }
}