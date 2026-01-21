# WareBotCollect_Group8
This project has been designed to implement an automated order-handling solution in an industrial context. It integrates a robot, GUI, database and order-handling logic.
The purpose of the system is to enable an admin to create and initiate orders by programmed pick-up and placement positiont. Once the order has been completed, the admin must confirm the completion to process the next order.

System overview.
The solution consists of four main parts:
1. GUI (used for login, order creation, overview of previous orders and completed order confirmation)
2. Database (stores users, orders and content in the order)
3. Order handling logic (handles order flow and communication between the GUI, database and robot)
4. Robot actions (sends the URScript program to the robot)

Functional description:
- The admin logs in through the GUI.
- Creates an order.
- The order is stored in the database.
- The order visible in the database tab.
- The admin initiates the processing of the order.
- The robot receives the program of pick-and-place positions.
- Admin confirms the order completion.
- The order is removed from the database tab and is visible as a previous order.

Project filestructure:
- README (overview)
- .sln (solution file)
- app.manifest (application)
- 2 x .axaml (graphical layout)
- 2 x .csproj (configuration)
- 3 x sqlite (using entity framework)
- 7 x .cs (logic, log in, order)

Program uses:
- C# 
- URScript
- GUI
- SQLite


Reference:
What you need to know about README Files | Lenovo US. (2023, May 28). https://www.lenovo.com/us/en/glossary/readme-file/?orgRef=https%253A%252F%252Fwww.google.com%252F
