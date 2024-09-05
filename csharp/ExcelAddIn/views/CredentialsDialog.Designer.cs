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
      corePlusPanel = new Panel();
      validateCertCheckBox = new CheckBox();
      operateAsBox = new TextBox();
      passwordBox = new TextBox();
      label5 = new Label();
      label4 = new Label();
      userIdBox = new TextBox();
      userIdLabel = new Label();
      jsonUrlBox = new TextBox();
      label3 = new Label();
      corePanel = new Panel();
      connectionStringBox = new TextBox();
      label2 = new Label();
      groupBox1 = new GroupBox();
      sessionTypeIsGroovyButton = new RadioButton();
      sessionTypeIsPythonButton = new RadioButton();
      finalPanel = new Panel();
      makeDefaultCheckBox = new CheckBox();
      testResultsTextBox = new TextBox();
      testResultsLabel = new Label();
      testCredentialsButton = new Button();
      setCredentialsButton = new Button();
      isCorePlusRadioButton = new RadioButton();
      isCoreRadioButton = new RadioButton();
      endpointIdBox = new TextBox();
      label1 = new Label();
      connectionTypeGroup = new GroupBox();
      connectionIdPanel = new Panel();
      corePlusPanel.SuspendLayout();
      corePanel.SuspendLayout();
      groupBox1.SuspendLayout();
      finalPanel.SuspendLayout();
      connectionTypeGroup.SuspendLayout();
      connectionIdPanel.SuspendLayout();
      SuspendLayout();
      // 
      // corePlusPanel
      // 
      corePlusPanel.Controls.Add(validateCertCheckBox);
      corePlusPanel.Controls.Add(operateAsBox);
      corePlusPanel.Controls.Add(passwordBox);
      corePlusPanel.Controls.Add(label5);
      corePlusPanel.Controls.Add(label4);
      corePlusPanel.Controls.Add(userIdBox);
      corePlusPanel.Controls.Add(userIdLabel);
      corePlusPanel.Controls.Add(jsonUrlBox);
      corePlusPanel.Controls.Add(label3);
      corePlusPanel.Dock = DockStyle.Top;
      corePlusPanel.Location = new Point(0, 191);
      corePlusPanel.Name = "corePlusPanel";
      corePlusPanel.Size = new Size(1054, 299);
      corePlusPanel.TabIndex = 210;
      // 
      // validateCertCheckBox
      // 
      validateCertCheckBox.AutoSize = true;
      validateCertCheckBox.Location = new Point(194, 237);
      validateCertCheckBox.Name = "validateCertCheckBox";
      validateCertCheckBox.RightToLeft = RightToLeft.No;
      validateCertCheckBox.Size = new Size(183, 29);
      validateCertCheckBox.TabIndex = 216;
      validateCertCheckBox.Text = "Validate Certificate";
      validateCertCheckBox.UseVisualStyleBackColor = true;
      // 
      // operateAsBox
      // 
      operateAsBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
      operateAsBox.Location = new Point(194, 180);
      operateAsBox.Name = "operateAsBox";
      operateAsBox.Size = new Size(795, 31);
      operateAsBox.TabIndex = 214;
      // 
      // passwordBox
      // 
      passwordBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
      passwordBox.Location = new Point(194, 130);
      passwordBox.Name = "passwordBox";
      passwordBox.PasswordChar = '●';
      passwordBox.Size = new Size(795, 31);
      passwordBox.TabIndex = 213;
      // 
      // label5
      // 
      label5.AutoSize = true;
      label5.Location = new Point(84, 183);
      label5.Name = "label5";
      label5.Size = new Size(96, 25);
      label5.TabIndex = 5;
      label5.Text = "OperateAs";
      // 
      // label4
      // 
      label4.AutoSize = true;
      label4.Location = new Point(84, 133);
      label4.Name = "label4";
      label4.Size = new Size(87, 25);
      label4.TabIndex = 4;
      label4.Text = "Password";
      // 
      // userIdBox
      // 
      userIdBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
      userIdBox.Location = new Point(194, 82);
      userIdBox.Name = "userIdBox";
      userIdBox.Size = new Size(795, 31);
      userIdBox.TabIndex = 212;
      // 
      // userIdLabel
      // 
      userIdLabel.AutoSize = true;
      userIdLabel.Location = new Point(108, 88);
      userIdLabel.Name = "userIdLabel";
      userIdLabel.Size = new Size(63, 25);
      userIdLabel.TabIndex = 2;
      userIdLabel.Text = "UserId";
      // 
      // jsonUrlBox
      // 
      jsonUrlBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
      jsonUrlBox.Location = new Point(194, 27);
      jsonUrlBox.Name = "jsonUrlBox";
      jsonUrlBox.Size = new Size(795, 31);
      jsonUrlBox.TabIndex = 211;
      // 
      // label3
      // 
      label3.AutoSize = true;
      label3.Location = new Point(89, 30);
      label3.Name = "label3";
      label3.Size = new Size(91, 25);
      label3.TabIndex = 0;
      label3.Text = "JSON URL";
      // 
      // corePanel
      // 
      corePanel.Controls.Add(connectionStringBox);
      corePanel.Controls.Add(label2);
      corePanel.Controls.Add(groupBox1);
      corePanel.Dock = DockStyle.Top;
      corePanel.Location = new Point(0, 490);
      corePanel.Name = "corePanel";
      corePanel.Size = new Size(1054, 170);
      corePanel.TabIndex = 220;
      // 
      // connectionStringBox
      // 
      connectionStringBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
      connectionStringBox.Location = new Point(194, 20);
      connectionStringBox.Name = "connectionStringBox";
      connectionStringBox.Size = new Size(795, 31);
      connectionStringBox.TabIndex = 221;
      // 
      // label2
      // 
      label2.AutoSize = true;
      label2.Location = new Point(27, 23);
      label2.Name = "label2";
      label2.Size = new Size(153, 25);
      label2.TabIndex = 0;
      label2.Text = "Connection String";
      // 
      // groupBox1
      // 
      groupBox1.Controls.Add(sessionTypeIsGroovyButton);
      groupBox1.Controls.Add(sessionTypeIsPythonButton);
      groupBox1.Location = new Point(18, 80);
      groupBox1.Name = "groupBox1";
      groupBox1.Size = new Size(970, 71);
      groupBox1.TabIndex = 231;
      groupBox1.TabStop = false;
      groupBox1.Text = "Session Type";
      // 
      // sessionTypeIsGroovyButton
      // 
      sessionTypeIsGroovyButton.AutoSize = true;
      sessionTypeIsGroovyButton.Location = new Point(338, 30);
      sessionTypeIsGroovyButton.Name = "sessionTypeIsGroovyButton";
      sessionTypeIsGroovyButton.Size = new Size(95, 29);
      sessionTypeIsGroovyButton.TabIndex = 1;
      sessionTypeIsGroovyButton.TabStop = true;
      sessionTypeIsGroovyButton.Text = "Groovy";
      sessionTypeIsGroovyButton.UseVisualStyleBackColor = true;
      // 
      // sessionTypeIsPythonButton
      // 
      sessionTypeIsPythonButton.AutoSize = true;
      sessionTypeIsPythonButton.Location = new Point(192, 30);
      sessionTypeIsPythonButton.Name = "sessionTypeIsPythonButton";
      sessionTypeIsPythonButton.Size = new Size(93, 29);
      sessionTypeIsPythonButton.TabIndex = 0;
      sessionTypeIsPythonButton.TabStop = true;
      sessionTypeIsPythonButton.Text = "Python";
      sessionTypeIsPythonButton.UseVisualStyleBackColor = true;
      // 
      // finalPanel
      // 
      finalPanel.Controls.Add(makeDefaultCheckBox);
      finalPanel.Controls.Add(testResultsTextBox);
      finalPanel.Controls.Add(testResultsLabel);
      finalPanel.Controls.Add(testCredentialsButton);
      finalPanel.Controls.Add(setCredentialsButton);
      finalPanel.Dock = DockStyle.Bottom;
      finalPanel.Location = new Point(0, 492);
      finalPanel.Name = "finalPanel";
      finalPanel.Size = new Size(1054, 98);
      finalPanel.TabIndex = 230;
      // 
      // makeDefaultCheckBox
      // 
      makeDefaultCheckBox.Anchor = AnchorStyles.Top | AnchorStyles.Right;
      makeDefaultCheckBox.AutoSize = true;
      makeDefaultCheckBox.Location = new Point(640, 47);
      makeDefaultCheckBox.Name = "makeDefaultCheckBox";
      makeDefaultCheckBox.Size = new Size(143, 29);
      makeDefaultCheckBox.TabIndex = 234;
      makeDefaultCheckBox.Text = "Make Default";
      makeDefaultCheckBox.UseVisualStyleBackColor = true;
      // 
      // testResultsTextBox
      // 
      testResultsTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
      testResultsTextBox.Location = new Point(187, 0);
      testResultsTextBox.Name = "testResultsTextBox";
      testResultsTextBox.ReadOnly = true;
      testResultsTextBox.Size = new Size(801, 31);
      testResultsTextBox.TabIndex = 7;
      // 
      // testResultsLabel
      // 
      testResultsLabel.AutoSize = true;
      testResultsLabel.Location = new Point(157, 48);
      testResultsLabel.Name = "testResultsLabel";
      testResultsLabel.Size = new Size(0, 25);
      testResultsLabel.TabIndex = 6;
      // 
      // testCredentialsButton
      // 
      testCredentialsButton.Location = new Point(46, 0);
      testCredentialsButton.Name = "testCredentialsButton";
      testCredentialsButton.Size = new Size(134, 34);
      testCredentialsButton.TabIndex = 231;
      testCredentialsButton.Text = "Test Creds";
      testCredentialsButton.UseVisualStyleBackColor = true;
      testCredentialsButton.Click += testCredentialsButton_Click;
      // 
      // setCredentialsButton
      // 
      setCredentialsButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
      setCredentialsButton.Location = new Point(789, 44);
      setCredentialsButton.Name = "setCredentialsButton";
      setCredentialsButton.Size = new Size(200, 34);
      setCredentialsButton.TabIndex = 232;
      setCredentialsButton.Text = "Set Credentials";
      setCredentialsButton.UseVisualStyleBackColor = true;
      setCredentialsButton.Click += setCredentialsButton_Click;
      // 
      // isCorePlusRadioButton
      // 
      isCorePlusRadioButton.AutoSize = true;
      isCorePlusRadioButton.Location = new Point(192, 30);
      isCorePlusRadioButton.Name = "isCorePlusRadioButton";
      isCorePlusRadioButton.Size = new Size(169, 29);
      isCorePlusRadioButton.TabIndex = 110;
      isCorePlusRadioButton.TabStop = true;
      isCorePlusRadioButton.Text = "Enterprise Core+";
      isCorePlusRadioButton.UseVisualStyleBackColor = true;
      // 
      // isCoreRadioButton
      // 
      isCoreRadioButton.AutoSize = true;
      isCoreRadioButton.Location = new Point(388, 30);
      isCoreRadioButton.Name = "isCoreRadioButton";
      isCoreRadioButton.Size = new Size(172, 29);
      isCoreRadioButton.TabIndex = 111;
      isCoreRadioButton.TabStop = true;
      isCoreRadioButton.Text = "Community Core";
      isCoreRadioButton.UseVisualStyleBackColor = true;
      // 
      // endpointIdBox
      // 
      endpointIdBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
      endpointIdBox.Location = new Point(194, 29);
      endpointIdBox.Name = "endpointIdBox";
      endpointIdBox.Size = new Size(795, 31);
      endpointIdBox.TabIndex = 1;
      // 
      // label1
      // 
      label1.AutoSize = true;
      label1.Location = new Point(46, 29);
      label1.Name = "label1";
      label1.Size = new Size(125, 25);
      label1.TabIndex = 5;
      label1.Text = "Connection ID";
      // 
      // connectionTypeGroup
      // 
      connectionTypeGroup.Controls.Add(isCorePlusRadioButton);
      connectionTypeGroup.Controls.Add(isCoreRadioButton);
      connectionTypeGroup.Location = new Point(34, 84);
      connectionTypeGroup.Name = "connectionTypeGroup";
      connectionTypeGroup.Size = new Size(588, 80);
      connectionTypeGroup.TabIndex = 100;
      connectionTypeGroup.TabStop = false;
      connectionTypeGroup.Text = "Connection Type";
      // 
      // connectionIdPanel
      // 
      connectionIdPanel.Controls.Add(label1);
      connectionIdPanel.Controls.Add(endpointIdBox);
      connectionIdPanel.Controls.Add(connectionTypeGroup);
      connectionIdPanel.Dock = DockStyle.Top;
      connectionIdPanel.Location = new Point(0, 0);
      connectionIdPanel.Name = "connectionIdPanel";
      connectionIdPanel.Size = new Size(1054, 191);
      connectionIdPanel.TabIndex = 231;
      // 
      // CredentialsDialog
      // 
      AutoScaleDimensions = new SizeF(10F, 25F);
      AutoScaleMode = AutoScaleMode.Font;
      ClientSize = new Size(1054, 590);
      Controls.Add(finalPanel);
      Controls.Add(corePanel);
      Controls.Add(corePlusPanel);
      Controls.Add(connectionIdPanel);
      Name = "CredentialsDialog";
      Text = "Credentials Editor";
      corePlusPanel.ResumeLayout(false);
      corePlusPanel.PerformLayout();
      corePanel.ResumeLayout(false);
      corePanel.PerformLayout();
      groupBox1.ResumeLayout(false);
      groupBox1.PerformLayout();
      finalPanel.ResumeLayout(false);
      finalPanel.PerformLayout();
      connectionTypeGroup.ResumeLayout(false);
      connectionTypeGroup.PerformLayout();
      connectionIdPanel.ResumeLayout(false);
      connectionIdPanel.PerformLayout();
      ResumeLayout(false);
    }

    #endregion
    private RadioButton isCorePlusRadioButton;
    private RadioButton isCoreRadioButton;
    private Button setCredentialsButton;
    private TextBox endpointIdBox;
    private Label label1;
    private GroupBox connectionTypeGroup;
    private Panel corePlusPanel;
    private Panel corePanel;
    private Label label3;
    private Label label2;
    private TextBox jsonUrlBox;
    private TextBox connectionStringBox;
    private Label label4;
    private TextBox userIdBox;
    private Label userIdLabel;
    private TextBox operateAsBox;
    private TextBox passwordBox;
    private Label label5;
    private Panel finalPanel;
    private Button testCredentialsButton;
    private Label testResultsLabel;
    private TextBox testResultsTextBox;
    private CheckBox makeDefaultCheckBox;
    private CheckBox validateCertCheckBox;
    private GroupBox groupBox1;
    private RadioButton sessionTypeIsGroovyButton;
    private RadioButton sessionTypeIsPythonButton;
    private Panel connectionIdPanel;
  }
}