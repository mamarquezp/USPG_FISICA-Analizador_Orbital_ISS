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
using System.Net.Http;
using System.Text.Json; // Para System.Text.Json
using System.Text.Json.Serialization; // Para [JsonPropertyName]
using System.Threading.Tasks;
using System.Windows.Forms;

// --- Clases para deserializar (convertir) el JSON de la API ---

// Mapea el objeto "iss_position" dentro del JSON
public class IssPosition
{
    [JsonPropertyName("latitude")]
    public string Latitude { get; set; }

    [JsonPropertyName("longitude")]
    public string Longitude { get; set; }
}

// Mapea la respuesta principal de la API
public class IssApiResponse
{
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }

    [JsonPropertyName("iss_position")]
    public IssPosition IssPosition { get; set; }
}

// --- Formulario Principal de la Aplicación ---
public class IssTrackerForm : Form
{
    // --- Constantes Físicas para Simulación ---
    private const double G = 6.67430e-11; // Constante de gravitación universal (m^3 kg^-1 s^-2)
    private const double M_EARTH = 5.97219e24; // Masa de la Tierra (kg)
    private const double R_EARTH_KM = 6371.0; // Radio medio de la Tierra (km)
    private const double R_EARTH_M = R_EARTH_KM * 1000.0; // Radio medio de la Tierra (m)

    // --- Componentes de la Interfaz (UI) ---
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
    private Label lblApiError; // Para mostrar errores de API

    // --- Variables para lógica ---
    private HttpClient httpClient;
    private Timer apiTimer;
    private double lastLat = 0;
    private double lastLon = 0;
    private long lastTimestamp = 0;
    private string mapImageUrl = "https://upload.wikimedia.org/wikipedia/commons/8/83/Equirectangular_projection_SW.jpg";

    public IssTrackerForm()
    {
        // Configuración del Formulario principal
        this.Text = "Analizador Orbital de la ISS (Proyecto Física 2)";
        this.Size = new Size(800, 700);
        this.FormBorderStyle = FormBorderStyle.FixedSingle;
        this.MaximizeBox = false;
        this.Load += IssTrackerForm_Load; // Añadir evento Load

        // Inicializar el cliente HTTP
        httpClient = new HttpClient();

        // --- Inicializar Componentes de la UI ---
        InitializeComponents();

        // Configurar el Timer para la API
        apiTimer = new Timer();
        apiTimer.Interval = 5000; // Consultar la API cada 5 segundos
        apiTimer.Tick += ApiTimer_Tick;
    }

    private void InitializeComponents()
    {
        // --- 1. Panel del Mapa ---
        picMap = new PictureBox
        {
            Location = new Point(10, 10),
            Size = new Size(760, 380), // Mapa 2:1
            SizeMode = PictureBoxSizeMode.StretchImage,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.LightGray
        };
        this.Controls.Add(picMap);

        // Icono para la ISS (un simple punto rojo)
        picIssIcon = new PictureBox
        {
            Size = new Size(12, 12),
            BackColor = Color.Red,
            Visible = false // Oculto hasta que tengamos datos
        };
        // Hacemos que el icono sea un "hijo" del mapa para que las coordenadas sean relativas
        picMap.Controls.Add(picIssIcon);


        // --- 2. Panel de Datos en Tiempo Real ---
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

        lblApiError = new Label
        {
            Text = "",
            Location = new Point(10, 630),
            AutoSize = true,
            ForeColor = Color.Red
        };
        this.Controls.Add(lblApiError);

        // --- 3. Panel de Simulación Física ---
        lblTitleSimulation = new Label
        {
            Text = "Simulador Orbital",
            Location = new Point(400, 400),
            Font = new Font("Arial", 12, FontStyle.Bold),
            AutoSize = true
        };
        this.Controls.Add(lblTitleSimulation);

        // Slider (TrackBar) vertical
        sliderAltitude = new TrackBar
        {
            Location = new Point(400, 430),
            Size = new Size(50, 180),
            Orientation = Orientation.Vertical,
            Minimum = 300,  // Altitud mínima (km)
            Maximum = 1000, // Altitud máxima (km)
            Value = 408,    // Altitud media real de la ISS
            TickFrequency = 50,
            TickStyle = TickStyle.Both
        };
        sliderAltitude.Scroll += SliderAltitude_Scroll; // Añadir evento Scroll
        this.Controls.Add(sliderAltitude);

        // Etiquetas para la simulación
        lblSimAltitude = new Label { Text = "Altitud: 408 km", Location = new Point(460, 430), AutoSize = true, Font = new Font("Arial", 10, FontStyle.Bold) };
        lblSimVelocity = new Label { Text = "Velocidad Orbital: --.-- km/s", Location = new Point(460, 470), AutoSize = true };
        lblSimPeriod = new Label { Text = "Período (T): --.-- min", Location = new Point(460, 490), AutoSize = true };
        lblSimOrbitsPerDay = new Label { Text = "Vueltas por día: --.--", Location = new Point(460, 510), AutoSize = true };
        this.Controls.Add(lblSimAltitude);
        this.Controls.Add(lblSimVelocity);
        this.Controls.Add(lblSimPeriod);
        this.Controls.Add(lblSimOrbitsPerDay);
    }

    // --- Eventos ---

    private async void IssTrackerForm_Load(object sender, EventArgs e)
    {
        // Cargar la imagen del mapa de forma asíncrona
        try
        {
            picMap.LoadAsync(mapImageUrl);
        }
        catch (Exception ex)
        {
            picMap.BackColor = Color.DarkGray;
            MessageBox.Show($"No se pudo cargar el mapa: {ex.Message}");
        }

        // Calcular los datos de simulación iniciales
        CalculateSimulation(sliderAltitude.Value);

        // Iniciar el temporizador
        apiTimer.Start();
        // Llamar una vez al inicio para no esperar 5 segundos
        await UpdateIssLocation();
    }

    // El temporizador llama a esta función cada 5 segundos
    private async void ApiTimer_Tick(object sender, EventArgs e)
    {
        await UpdateIssLocation();
    }

    // El slider llama a esta función cada vez que se mueve
    private void SliderAltitude_Scroll(object sender, EventArgs e)
    {
        // Obtener el valor del slider
        int altitudeKm = sliderAltitude.Value;
        // Calcular la simulación con el nuevo valor
        CalculateSimulation(altitudeKm);
    }

    // --- Lógica Principal ---

    // 1. LÓGICA DE API Y DATOS EN TIEMPO REAL
    private async Task UpdateIssLocation()
    {
        string apiUrl = "http://api.open-notify.org/iss-now.json";
        try
        {
            // 1. Llamar a la API
            string jsonResponse = await httpClient.GetStringAsync(apiUrl);

            // 2. Deserializar el JSON
            IssApiResponse apiData = JsonSerializer.Deserialize<IssApiResponse>(jsonResponse);

            // 3. Convertir los datos
            double lat = double.Parse(apiData.IssPosition.Latitude);
            double lon = double.Parse(apiData.IssPosition.Longitude);
            long timestamp = apiData.Timestamp;

            // 4. Actualizar la UI
            lblLat.Text = $"Latitud: {lat:F4}";
            lblLon.Text = $"Longitud: {lon:F4}";
            lblApiError.Text = ""; // Limpiar errores

            // 5. Mover el icono en el mapa
            UpdateMapIcon(lat, lon);

            // 6. Calcular la velocidad (si no es la primera vez)
            if (lastTimestamp != 0)
            {
                double timeDiffSeconds = timestamp - lastTimestamp;

                // Evitar división por cero si la API responde muy rápido
                if (timeDiffSeconds > 0)
                {
                    // Calcular distancia usando la fórmula de Haversine
                    double distanceKm = Haversine(lastLat, lastLon, lat, lon);
                    double velocityKps = distanceKm / timeDiffSeconds;
                    lblVelocity.Text = $"Velocidad: {velocityKps:F2} km/s";
                }
            }

            // 7. Guardar los valores actuales para el próximo cálculo
            lastLat = lat;
            lastLon = lon;
            lastTimestamp = timestamp;
        }
        catch (Exception ex)
        {
            // Manejar errores de conexión
            lblApiError.Text = $"Error de API: {ex.Message}";
            lastTimestamp = 0; // Reiniciar para el cálculo de velocidad
        }
    }

    // 2. LÓGICA DE SIMULACIÓN FÍSICA
    private void CalculateSimulation(int altitudeKm)
    {
        // Convertir la altitud a metros
        double altitudeM = altitudeKm * 1000.0;

        // Calcular el radio total de la órbita (Radio Tierra + altitud)
        double r = R_EARTH_M + altitudeM;

        // --- FÓRMULAS FÍSICAS ---

        // 1. Velocidad Orbital (v)
        // v = sqrt(G * M / r)
        double velocityMps = Math.Sqrt((G * M_EARTH) / r);
        double velocityKps = velocityMps / 1000.0;

        // 2. Período Orbital (T) - tiempo para una vuelta
        // T = 2 * PI * sqrt(r^3 / (G * M))
        double periodSeconds = 2 * Math.PI * Math.Sqrt(Math.Pow(r, 3) / (G * M_EARTH));
        double periodMinutes = periodSeconds / 60.0;

        // 3. Vueltas por día
        double secondsPerDay = 86400.0;
        double orbitsPerDay = secondsPerDay / periodSeconds;

        // --- Actualizar UI de Simulación ---
        lblSimAltitude.Text = $"Altitud: {altitudeKm} km";
        lblSimVelocity.Text = $"Velocidad Orbital: {velocityKps:F2} km/s";
        lblSimPeriod.Text = $"Período (T): {periodMinutes:F2} min";
        lblSimOrbitsPerDay.Text = $"Vueltas por día: {orbitsPerDay:F2}";
    }

    // --- Funciones de Ayuda (Helpers) ---

    // Convierte (lat, lon) a coordenadas (x, y) en un mapa plano
    private void UpdateMapIcon(double lat, double lon)
    {
        // Tamaño del control PictureBox del mapa
        int mapWidth = picMap.ClientSize.Width;
        int mapHeight = picMap.ClientSize.Height;

        // Fórmula de proyección equirectangular
        int x = (int)((lon + 180.0) * (mapWidth / 360.0));
        int y = (int)((90.0 - lat) * (mapHeight / 180.0));

        // Centrar el icono en la coordenada (restar la mitad de su tamaño)
        picIssIcon.Location = new Point(x - (picIssIcon.Width / 2), y - (picIssIcon.Height / 2));
        picIssIcon.Visible = true;
        picIssIcon.BringToFront(); // Asegurarse de que esté visible
    }

    // Fórmula de Haversine para calcular la distancia entre dos puntos en una esfera
    private double Haversine(double lat1, double lon1, double lat2, double lon2)
    {
        // Convertir grados a radianes
        double ToRad(double deg) => deg * (Math.PI / 180.0);

        lat1 = ToRad(lat1);
        lon1 = ToRad(lon1);
        lat2 = ToRad(lat2);
        lon2 = ToRad(lon2);

        double dLat = lat2 - lat1;
        double dLon = lon2 - lon1;

        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(lat1) * Math.Cos(lat2) *
                   Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        double c = 2 * Math.Asin(Math.Sqrt(a));

        // Distancia en kilómetros
        return R_EARTH_KM * c;
    }
}
