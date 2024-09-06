namespace ExcelAddIn.views {
  partial class DeephavenMessageBox {
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
      captionLabel = new Label();
      contentsBox = new TextBox();
      okButton = new Button();
      SuspendLayout();
      // 
      // captionLabel
      // 
      captionLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
      captionLabel.AutoSize = true;
      captionLabel.Font = new Font("Segoe UI", 20F, FontStyle.Regular, GraphicsUnit.Point, 0);
      captionLabel.Location = new Point(306, 32);
      captionLabel.Name = "captionLabel";
      captionLabel.Size = new Size(162, 54);
      captionLabel.TabIndex = 0;
      captionLabel.Text = "Caption";
      // 
      // contentsBox
      // 
      contentsBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
      contentsBox.Location = new Point(40, 122);
      contentsBox.Multiline = true;
      contentsBox.Name = "contentsBox";
      contentsBox.Size = new Size(716, 215);
      contentsBox.TabIndex = 1;
      // 
      // okButton
      // 
      okButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
      okButton.Location = new Point(644, 385);
      okButton.Name = "okButton";
      okButton.Size = new Size(112, 34);
      okButton.TabIndex = 2;
      okButton.Text = "button1";
      okButton.UseVisualStyleBackColor = true;
      // 
      // DeephavenMessageBox
      // 
      AutoScaleDimensions = new SizeF(10F, 25F);
      AutoScaleMode = AutoScaleMode.Font;
      ClientSize = new Size(800, 450);
      Controls.Add(okButton);
      Controls.Add(contentsBox);
      Controls.Add(captionLabel);
      Name = "DeephavenMessageBox";
      Text = "DeephavenMessageBox";
      ResumeLayout(false);
      PerformLayout();
    }

    #endregion

    private Label captionLabel;
    private TextBox contentsBox;
    private Button okButton;
  }
}