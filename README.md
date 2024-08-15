# Microservices Application with .NET 8, RabbitMQ, and PostgreSQL

## Table of Contents

- [Overview](#overview)
- [Prerequisites](#prerequisites)
- [Getting Started](#getting-started)
  - [Clone the Repository](#1-clone-the-repository)
  - [Build and Run the Application with Docker Compose](#2-build-and-run-the-application-with-docker-compose)
  - [Access the Services](#3-access-the-services)
  - [Running Database Migrations](#4-running-database-migrations)
  - [Testing the Services](#5-testing-the-services)
  - [Stopping the Application](#6-stopping-the-application)
- [Project Structure](#project-structure)
- [Configuration](#configuration)
  - [Database Configuration](#database-configuration)
  - [RabbitMQ Configuration](#rabbitmq-configuration)
- [Implemented Features](#implemented-features)
- [Future Enhancements](#future-enhancements)
- [License](#license)
- [Contact](#contact)

## Overview

This project is a microservices-based application built using .NET 8. It consists of three main services:

1. **OrderService**: Manages orders and communicates with RabbitMQ to send order-related messages.
2. **UserService**: Manages users and communicates with RabbitMQ to send user-related messages.
3. **NotificationService**: Listens to RabbitMQ messages from the `OrderService` and `UserService` and processes notifications accordingly.

All services are containerized using Docker and configured to run in a Docker Compose environment. The application uses PostgreSQL as the database and RabbitMQ as the message broker.

## Architecture

- **OrderService**: Handles CRUD operations for orders and publishes order events to RabbitMQ.
- **UserService**: Handles CRUD operations for users and publishes user events to RabbitMQ.
- **NotificationService**: Listens for events from RabbitMQ and generates notifications (mocked for this example).
- **PostgreSQL**: Used as the database for storing orders and users.
- **RabbitMQ**: Used as the message broker for communication between services.

## Prerequisites

To run this project locally, you need to have the following installed:

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker](https://www.docker.com/products/docker-desktop)
- [Docker Compose](https://docs.docker.com/compose/install/)

## Getting Started

### 1. Clone the Repository

```bash
git clone https://github.com/yourusername/microservices-project.git
cd microservices-project
```

### 2. Build and Run the Application with Docker Compose
Use Docker Compose to build and start all services:

```bash
docker-compose up --build
```

This command will:

 - Build the Docker images for OrderService, UserService, and NotificationService.
 - Start the services along with PostgreSQL and RabbitMQ.
 - Set up the necessary database schema and connections.

### 3. Access the Services
Once the services are running, you can access them at the following endpoints:

**OrderService**: ``http://localhost:8081``
**UserService**: ``http://localhost:8082``
**RabbitMQ Management UI**: ``http://localhost:15672`` (default username: guest, password: guest)

### 4. Running Database Migrations
If you need to apply database migrations, you can do so using the Entity Framework Core CLI:

```bash
docker-compose exec orderservice dotnet ef database update
docker-compose exec userservice dotnet ef database update
```

### 5. Testing the Services
You can test the services using tools like Postman or cURL. Here are some example requests:

 - Create a New Order:
```bash
curl -X POST http://localhost:8081/orders -H "Content-Type: application/json" -d '{"ProductName": "Product1", "Quantity": 2, "Price": 50.00}'
```

 - Create a New User:
```bash
curl -X POST http://localhost:8082/users -H "Content-Type: application/json" -d '{"FirstName": "John", "LastName": "Doe", "Email": "john.doe@example.com"}'

```

### 6. Stopping the Application
To stop all running services, use:
```bash
docker-compose down
```
This command stops and removes all the containers, networks, and volumes created by Docker Compose.

## Project Structure
 - **OrderService/**: Contains the source code for the ``OrderService``.
 - **UserService/**: Contains the source code for the ``UserService``.
 - **NotificationService/**: Contains the source code for the ``NotificationService``.
 - **Shared/**: Contains shared code and libraries used by multiple services.
 - **docker-compose.yml**: Docker Compose file to set up and run the services.

## Configuration
### Database Configuration
The PostgreSQL database is configured using environment variables in the ``docker-compose.yml`` file. You can modify the configuration as needed:

```yaml
postgres:
  environment:
    POSTGRES_USER: "postgres"
    POSTGRES_PASSWORD: "postgres"
    POSTGRES_DB: "mydb"
```

### RabbitMQ Configuration
RabbitMQ is also configured in the ``docker-compose.yml`` file:
```yaml
rabbitmq:
  image: "rabbitmq:3-management"
  ports:
    - "15672:15672" # RabbitMQ management UI
    - "5672:5672"   # RabbitMQ messaging port

```

## Implemented Features
 -**Soft Delete**: The OrderService and UserService implement a soft delete feature, which allows records to be marked as deleted without being physically removed from the database.
 - **Global Exception Handling**: Each service has global exception handling to ensure that unhandled exceptions are logged and managed gracefully.
 - **API Versioning**: The services are versioned to allow for future API changes without breaking existing clients.
 - **Health Checks**: Basic health checks are implemented to monitor the status of the services.

## Future Enhancements
 - **Implement Real Notifications**: Replace the mocked notification logic in NotificationService with real notification mechanisms (e.g., email, SMS).
 - **Add Authentication/Authorization**: Secure the services with OAuth2 or JWT-based authentication.
 - **Improve Logging and Monitoring**: Integrate with a centralized logging and monitoring system like ELK Stack or Prometheus/Grafana.

## License
This project is licensed under the MIT License - see the LICENSE file for details.

## Contact
For questions or support, please contact me.