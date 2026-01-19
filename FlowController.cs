using System;
using System.Collections.Generic;

namespace Login
{
    /// <summary>
    /// FlowController beskriver den overordnede proces for ordrebehandling.
    /// Klassen er IKKE et entry point og køres ikke direkte.
    /// Den bruges som proces-/flow-logik svarende til flowchartet.
    /// </summary>
    public static class FlowController
    {
        public static void RunFlowExample()
        {
            // Start på flowchart
            bool adminLoggedIn = true;

            // "Database" af ordrer – hver ordre er en liste af komponenter
            List<List<string>> orderDatabase = new List<List<string>>();

            // Første ordre (eksempel)
            AddOrderWithQty(orderDatabase);

            // Er der flere ordrer?
            while (adminLoggedIn)
            {
                // Ingen flere ordrer → admin logger ud
                if (orderDatabase.Count == 0)
                {
                    break;
                }

                // Load næste ordre
                List<string> currentOrder = orderDatabase[0];
                orderDatabase.RemoveAt(0);

                // Hvis ordren indeholder produkter
                if (currentOrder.Count > 0)
                {
                    bool pickOk = true;   // svarer til: robot pick OK?
                    bool placeOk = true;  // svarer til: robot place OK?

                    if (!pickOk)
                    {
                        // Gå til startposition + fejl
                        continue;
                    }

                    if (!placeOk)
                    {
                        // Placement fejlede
                        continue;
                    }
                }

                // Behandl alle produkter i ordren
                while (currentOrder.Count > 0)
                {
                    // Identificér komponent
                    string component = currentOrder[0];
                    currentOrder.RemoveAt(0);

                    // Her svarer det til:
                    // robot.run_by_name("A"/"B"/"C")
                }

                // Ordre færdig – admin bekræfter
                adminLoggedIn = false; // ingen ny ordre i eksemplet
            }
        }

        /// <summary>
        /// Tilføjer en ordre med komponenter og antal
        /// (bruges som eksempel/pseudologik)
        /// </summary>
        private static void AddOrderWithQty(List<List<string>> orderDatabase)
        {
            List<string> order = new List<string>();

            // Eksempelordre: A, C, A
            order.Add("A");
            order.Add("C");
            order.Add("A");

            orderDatabase.Add(order);
        }
    }
}
