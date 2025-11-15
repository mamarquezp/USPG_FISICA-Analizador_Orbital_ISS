namespace USPG_FISICA_Analizador_Orbital_ISS
{
    public static class Program
    {
        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            // Inicia el formulario principal de nuestra aplicación
            Application.Run(new IssTrackerForm());
        }
    }
}