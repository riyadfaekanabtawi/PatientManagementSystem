# Patient Management System

## Overview
This is a **Patient Management System** built using **ASP.NET Core MVC**. The system allows patient management, including uploading images and generating **3D face models** using the **Masterpiece X API**.

## Features
- **Patient Management** (Add, Edit, Delete patients)
- **Image Upload** (Front, Left, Right, Back images)
- **Generate 3D Model** via **Masterpiece X API**
- **View 3D Models** using **Three.js**
- **Adjust Face Features** (Cheeks, Chin, Nose adjustments)
- **Save Adjustments & History**

## Installation
### Prerequisites
Ensure you have the following installed:
- .NET SDK 8.0+
- SQL Server (or SQLite for local development)
- Node.js (for frontend dependencies if needed)
- Masterpiece X API Key (Sign up at https://developers.masterpiecex.com)

### Clone the Repository
```sh
git clone https://github.com/your-repo/patient-management-system.git
cd patient-management-system
```

### Configure Database
Modify `appsettings.json` with your **SQL Server connection string**:
```json
"ConnectionStrings": {
  "DefaultConnection": "Server=YOUR_SERVER;Database=PatientDB;User Id=YOUR_USER;Password=YOUR_PASSWORD;"
}
```

Run migrations to create the database:
```sh
dotnet ef database update
```

### Configure Masterpiece X API
Add your **API Key** to `appsettings.json`:
```json
"MasterpieceX": {
  "ApiKey": "YOUR_MASTERPIECE_X_API_KEY"
}
```

### Run the Application
```sh
dotnet run
```
Visit `http://localhost:5000` in your browser.

## Usage
### 1. **Add a New Patient**
- Navigate to `Patients/Create`
- Enter patient details & upload images
- Click **Save**

### 2. **Generate 3D Model**
- Go to `Patients/AdjustFace/{id}`
- Click **Generar Modelo 3D**
- Wait for processing (polls API every 5 seconds)
- Model loads in **Three.js**

### 3. **Adjust Face & Save**
- Use sliders to adjust **Cheeks, Chin, Nose**
- Add **Notes** (if needed)
- Click **Guardar Ajustes** to save

## API Endpoints
### **Patient Routes**
| Method | Route | Description |
|--------|--------------------------|-------------------------------|
| `GET`  | `/Patients` | List all patients |
| `POST` | `/Patients/Create` | Create new patient |
| `GET`  | `/Patients/Edit/{id}` | Edit patient details |
| `POST` | `/Patients/Delete/{id}` | Delete patient |

### **3D Model Generation**
| Method | Route | Description |
|--------|--------------------------------|-------------------------------|
| `POST` | `/Patients/Generate3DModel/{id}` | Generate a 3D model |
| `POST` | `/Patients/Save3DModel/{id}` | Save generated model URL |

## Technologies Used
- **Backend:** ASP.NET Core MVC, Entity Framework Core
- **Frontend:** Razor, Bootstrap, JavaScript (Three.js)
- **Database:** SQL Server / SQLite (for development)
- **3D Processing:** Masterpiece X API
- **Deployment:** AWS EC2 / Azure App Service (optional)

## Deployment
To deploy on **AWS EC2**:
```sh
dotnet publish -c Release -o out
scp -r out/ ubuntu@your-server-ip:/var/www/patientapp
```
Restart the service:
```sh
sudo systemctl restart patientapp
```

## Future Improvements
- ✅ Add support for **multiple image views** (Left, Right, Back)
- ✅ Implement **authentication & user roles**
- ✅ Improve **3D model visualization** with better UI

## Contributing
- Fork the repo
- Create a feature branch
- Submit a pull request!

## License
This project is licensed under the **MIT License**.

