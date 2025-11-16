/*
 * PROYECTO FINAL DE FÍSICA 2: ANALIZADOR ORBITAL DE LA ISS
 * * Este archivo contiene una aplicación completa de Windows Forms en C#
 * que cumple con los siguientes objetivos:
 * * 1.  Conexión en tiempo real: Llama a la API de Open-Notify para obtener la 
 * latitud y longitud de la Estación Espacial Internacional (ISS).
 * 2.  Cálculo de velocidad: Estima la velocidad instantánea de la ISS 
 * calculando la distancia (fórmula de Haversine) entre dos puntos 
 * y dividiéndola por el tiempo.
 * 3.  Visualización en mapa: Proyecta las coordenadas (lat, lon) en un 
 * mapa mundi plano (proyección equirectangular).
 * 4.  Simulación física: Utiliza un slider para simular diferentes 
 * altitudes y calcula la velocidad orbital, el período y el 
 * número de vueltas a la Tierra por día para esa altitud.
 * * Todo el código está contenido en este único archivo para facilitar su 
 * implementación y revisión.
 */

using System;
using System.Drawing;
using System.Net;
using System.Net.Http;
using System.Text.Json; // Para System.Text.Json
using System.Text.Json.Serialization; // Para [JsonPropertyName]
using System.Threading.Tasks;
using System.Windows.Forms;

namespace USPG_FISICA_Analizador_Orbital_ISS
{
    // --- Clases para deserializar (convertir) el JSON de la API ---
    public class IssPosition
    {
        [JsonPropertyName("latitude")]
        public string? Latitude { get; set; }

        [JsonPropertyName("longitude")]
        public string? Longitude { get; set; }
    }

    public class IssApiResponse
    {
        [JsonPropertyName("timestamp")]
        public long Timestamp { get; set; }

        [JsonPropertyName("iss_position")]
        public IssPosition? IssPosition { get; set; }
    }

    // --- Formulario Principal ---
    public class IssTrackerForm : Form
    {
        // --- Constantes Físicas ---
        private const double G = 6.67430e-11;
        private const double M_EARTH = 5.97219e24;
        private const double R_EARTH_KM = 6371.0;
        private const double R_EARTH_M = R_EARTH_KM * 1000.0;

        // --- UI ---
        private PictureBox picMap;
        private PictureBox picIssIcon;
        private Label lblTitleRealTime;
        private Label lblLat;
        private Label lblLon;
        private Label lblVelocity;
        private Label lblTitleSimulation;
        private TrackBar sliderAltitude;
        private Label lblSimAltitude;
        private Label lblSimVelocity;
        private Label lblSimPeriod;
        private Label lblSimOrbitsPerDay;
        private Label lblApiError;

        // --- Lógica ---
        private HttpClient httpClient;
        private System.Windows.Forms.Timer apiTimer;
        private double lastLat = 0;
        private double lastLon = 0;
        private long lastTimestamp = 0;

        public IssTrackerForm()
        {
            this.Text = "Analizador Orbital de la ISS - Física 2";
            this.Size = new Size(800, 700);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.Load += IssTrackerForm_Load;

            // Protocolos de seguridad para la API
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;

            httpClient = new HttpClient();
            InitializeComponents();

            apiTimer = new System.Windows.Forms.Timer();
            apiTimer.Interval = 5000;
            apiTimer.Tick += ApiTimer_Tick;
        }

        private void InitializeComponents()
        {
            // 1. MAPA
            picMap = new PictureBox
            {
                Location = new Point(10, 10),
                Size = new Size(760, 380),
                SizeMode = PictureBoxSizeMode.StretchImage,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.LightGray
            };
            this.Controls.Add(picMap);

            // ICONO ISS
            picIssIcon = new PictureBox
            {
                Size = new Size(12, 12),
                BackColor = Color.Red,
                Visible = false
            };
            picMap.Controls.Add(picIssIcon);

            // 2. DATOS REALES
            lblTitleRealTime = new Label
            {
                Text = "Datos en Tiempo Real (API Open-Notify)",
                Location = new Point(10, 400),
                Font = new Font("Arial", 12, FontStyle.Bold),
                AutoSize = true
            };
            this.Controls.Add(lblTitleRealTime);

            lblLat = new Label { Text = "Latitud: --.--", Location = new Point(15, 430), AutoSize = true };
            lblLon = new Label { Text = "Longitud: --.--", Location = new Point(15, 450), AutoSize = true };
            lblVelocity = new Label { Text = "Velocidad: --.-- km/s", Location = new Point(15, 470), AutoSize = true };
            this.Controls.Add(lblLat);
            this.Controls.Add(lblLon);
            this.Controls.Add(lblVelocity);

            lblApiError = new Label { Text = "", Location = new Point(10, 630), AutoSize = true, ForeColor = Color.Red };
            this.Controls.Add(lblApiError);

            // 3. SIMULADOR
            lblTitleSimulation = new Label
            {
                Text = "Simulador Orbital",
                Location = new Point(400, 400),
                Font = new Font("Arial", 12, FontStyle.Bold),
                AutoSize = true
            };
            this.Controls.Add(lblTitleSimulation);

            sliderAltitude = new TrackBar
            {
                Location = new Point(400, 430),
                Size = new Size(50, 180),
                Orientation = Orientation.Vertical,
                Minimum = 0,
                Maximum = 1000,
                Value = 408,
                TickFrequency = 50,
                TickStyle = TickStyle.Both
            };
            sliderAltitude.Scroll += SliderAltitude_Scroll;
            this.Controls.Add(sliderAltitude);

            lblSimAltitude = new Label { Text = "Altitud: 408 km", Location = new Point(460, 430), AutoSize = true, Font = new Font("Arial", 10, FontStyle.Bold) };
            lblSimVelocity = new Label { Text = "Velocidad Orbital: --.-- km/s", Location = new Point(460, 470), AutoSize = true };
            lblSimPeriod = new Label { Text = "Período (T): --.-- min", Location = new Point(460, 490), AutoSize = true };
            lblSimOrbitsPerDay = new Label { Text = "Vueltas por día: --.--", Location = new Point(460, 510), AutoSize = true };
            this.Controls.Add(lblSimAltitude);
            this.Controls.Add(lblSimVelocity);
            this.Controls.Add(lblSimPeriod);
            this.Controls.Add(lblSimOrbitsPerDay);
        }

        private async void IssTrackerForm_Load(object sender, EventArgs e)
        {
            // --- CARGA DE IMAGEN DESDE RECURSOS ---
            try
            {
                // Aquí accedemos a la imagen
                picMap.Image = Properties.Resources.MapaMundi;
            }
            catch (Exception ex)
            {
                lblApiError.Text = "Error cargando recurso MapaMundi: " + ex.Message;
                picMap.BackColor = Color.Gray; // Color de fondo si falla
            }

            CalculateSimulation(sliderAltitude.Value);
            apiTimer.Start();
            await UpdateIssLocation();
        }

        private async void ApiTimer_Tick(object sender, EventArgs e)
        {
            await UpdateIssLocation();
        }

        private void SliderAltitude_Scroll(object sender, EventArgs e)
        {
            CalculateSimulation(sliderAltitude.Value);
        }

        // --- Lógica API ---
        private async Task UpdateIssLocation()
        {
            string apiUrl = "http://api.open-notify.org/iss-now.json";
            try
            {
                string jsonResponse = await httpClient.GetStringAsync(apiUrl);
                IssApiResponse? apiData = JsonSerializer.Deserialize<IssApiResponse>(jsonResponse);

                if (apiData != null && apiData.IssPosition != null)
                {
                    double lat = double.Parse(apiData.IssPosition.Latitude ?? "0");
                    double lon = double.Parse(apiData.IssPosition.Longitude ?? "0");
                    long timestamp = apiData.Timestamp;

                    lblLat.Text = $"Latitud: {lat:F4}";
                    lblLon.Text = $"Longitud: {lon:F4}";
                    lblApiError.Text = "";

                    UpdateMapIcon(lat, lon);

                    if (lastTimestamp != 0)
                    {
                        double timeDiffSeconds = timestamp - lastTimestamp;
                        if (timeDiffSeconds > 0)
                        {
                            double distanceKm = Haversine(lastLat, lastLon, lat, lon);
                            // Evitar picos irreales de velocidad por errores de GPS
                            if (distanceKm < 500)
                            {
                                double velocityKps = distanceKm / timeDiffSeconds;
                                lblVelocity.Text = $"Velocidad: {velocityKps:F2} km/s";
                            }
                        }
                    }
                    lastLat = lat;
                    lastLon = lon;
                    lastTimestamp = timestamp;
                }
            }
            catch (Exception ex)
            {
                lblApiError.Text = $"Error de API: {ex.Message}";
            }
        }

        // --- Simulación ---
        private void CalculateSimulation(int altitudeKm)
        {
            double altitudeM = altitudeKm * 1000.0;
            double r = R_EARTH_M + altitudeM;

            double velocityMps = Math.Sqrt((G * M_EARTH) / r);
            double velocityKps = velocityMps / 1000.0;

            double periodSeconds = 2 * Math.PI * Math.Sqrt(Math.Pow(r, 3) / (G * M_EARTH));
            double periodMinutes = periodSeconds / 60.0;

            double orbitsPerDay = 86400.0 / periodSeconds;

            lblSimAltitude.Text = $"Altitud: {altitudeKm} km";
            lblSimVelocity.Text = $"Velocidad Orbital: {velocityKps:F2} km/s";
            lblSimPeriod.Text = $"Período (T): {periodMinutes:F2} min";
            lblSimOrbitsPerDay.Text = $"Vueltas por día: {orbitsPerDay:F2}";
        }

        // --- Helpers ---
        private void UpdateMapIcon(double lat, double lon)
        {
            if (picMap.Image == null) return;

            int mapWidth = picMap.ClientSize.Width;
            int mapHeight = picMap.ClientSize.Height;

            // Ajuste simple para proyección equirectangular
            int x = (int)((lon + 180.0) * (mapWidth / 360.0));
            int y = (int)((90.0 - lat) * (mapHeight / 180.0));

            // Asegurar límites
            if (x < 0) x = 0; if (x >= mapWidth) x = mapWidth - 1;
            if (y < 0) y = 0; if (y >= mapHeight) y = mapHeight - 1;

            picIssIcon.Location = new Point(x - (picIssIcon.Width / 2), y - (picIssIcon.Height / 2));
            picIssIcon.Visible = true;
            picIssIcon.BringToFront();
        }

        private double Haversine(double lat1, double lon1, double lat2, double lon2)
        {
            double ToRad(double deg) => deg * (Math.PI / 180.0);
            lat1 = ToRad(lat1); lon1 = ToRad(lon1);
            lat2 = ToRad(lat2); lon2 = ToRad(lon2);

            double dLat = lat2 - lat1;
            double dLon = lon2 - lon1;

            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(lat1) * Math.Cos(lat2) *
                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            double c = 2 * Math.Asin(Math.Sqrt(a));
            return R_EARTH_KM * c;
        }
    }
}