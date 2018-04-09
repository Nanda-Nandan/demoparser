namespace DemoFileWatcher
{
    partial class ProjectInstaller
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.DocParserServiceProcessInstaller = new System.ServiceProcess.ServiceProcessInstaller();
            this.DocParserServiceInstaller = new System.ServiceProcess.ServiceInstaller();
            // 
            // DocParserServiceProcessInstaller
            // 
            this.DocParserServiceProcessInstaller.Account = System.ServiceProcess.ServiceAccount.LocalService;
            this.DocParserServiceProcessInstaller.Password = null;
            this.DocParserServiceProcessInstaller.Username = null;
            // 
            // DocParserServiceInstaller
            // 
            this.DocParserServiceInstaller.ServiceName = "DemoDocParser";
            // 
            // ProjectInstaller
            // 
            this.Installers.AddRange(new System.Configuration.Install.Installer[] {
            this.DocParserServiceProcessInstaller,
            this.DocParserServiceInstaller});

        }

        #endregion

        private System.ServiceProcess.ServiceProcessInstaller DocParserServiceProcessInstaller;
        private System.ServiceProcess.ServiceInstaller DocParserServiceInstaller;
    }
}