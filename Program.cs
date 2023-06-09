

using Npgsql;
using System.Reflection.PortableExecutable;
using System.Configuration;
using System.Xml.Linq;
using System.Data.SQLite;
using static Program;

internal class Program
{
    private static void Main(string[] args)
    {
        Console.WriteLine("EDReqAnalysis V0.1");
        Console.WriteLine("Por favor, seleccione una opción:");
        Console.WriteLine("0) Generar reporte general.");
        Console.WriteLine("1) Generar reporte de solicitudes desatendidas.");
        Console.WriteLine("2) Generar reporte de solicitudes con smells.");
        Console.WriteLine("3) Generar reporte de incongruencias.");
        Console.WriteLine(Environment.NewLine);
        Console.Write("Su opción: ");
        int option = Convert.ToInt32(Console.ReadLine());

        switch (option)
        {
            case 0:
                reportGeneral();
                break;
            case 1:
                reportUnattendedRequests();
                break;
            case 2:
                reportSmells();
                break;
            case 3:
                reportMismatch();
                break;
            default:
                Console.WriteLine("Opción inválida.");
                break;
        }
    }

    internal class Requirement
    {
        private int id;
        private string description;
        private Boolean with_smell, with_mismatch;
        private decimal comments_count, anger_count, joy_count, love_count, sadness_count;

        public int Id
        { get; set; }
        public string Description 
        { get; set; }
        public Boolean WithSmell 
        { get; set; }
        public Boolean WithMismatch
        { get; set; }
        public decimal CommentsCount
        { get; set; }
        public decimal AngerCount
        { get; set; }
        public decimal JoyCount
        { get; set; }
        public decimal LoveCount
        { get; set; }
        public decimal SadnessCount 
        { get; set; }

        public Requirement(int id, string description)
        {
            this.Id = id;
            this.Description = description;
            this.WithSmell = false;
            this.WithMismatch = false;
            this.CommentsCount = 0;
            this.AngerCount = 0;
            this.JoyCount = 0;
            this.LoveCount = 0;
            this.SadnessCount = 0;
        }
    }

    internal class Smell
    {
        private int id;
        private string name;
        private decimal count;
        private List<string> phrases;

        public int Id
        { get; set; }

        public string Name
        { get; set; }

        public decimal Count
        { get; set; }

        public List<string> Phrases
        { get; set; }

        public Smell(int id, string name)
        {
            this.Id = id;
            this.Name = name;
            this.Count = 0;
            this.Phrases = new List<string>();
        }
    }

    private static void reportGeneral()
    {
        Console.WriteLine(Environment.NewLine);
        Console.WriteLine("Generando reporte general");

        ConnectionStringSettings settings = ConfigurationManager.ConnectionStrings["jira"];
        using (NpgsqlConnection conn = new NpgsqlConnection(settings.ConnectionString))
        {
            conn.Open();

            NpgsqlCommand command;
            NpgsqlDataReader reader;
            string query;

            List<Requirement> requirements = new List<Requirement>();
            query = "SELECT id, description from jira_issue_report WHERE trim(description) <> '' AND reporter_id IS NOT NULL AND reporter_id > 0";
            command = new NpgsqlCommand(query, conn);
            reader = command.ExecuteReader();
            while (reader.Read())
                requirements.Add(new Requirement(Convert.ToInt32(reader["id"]), Convert.ToString(reader["description"])));
            reader.Close();

            Console.WriteLine("Total de solicitudes: {0}", requirements.Count);
            commentsAnalysis(requirements);
        }
    }

    private static void reportUnattendedRequests()
    {
        Console.WriteLine(Environment.NewLine);
        Console.WriteLine("Generando reporte de solicitudes desatendidas");

        ConnectionStringSettings settings = ConfigurationManager.ConnectionStrings["jira"];
        using (NpgsqlConnection conn = new NpgsqlConnection(settings.ConnectionString))
        {
            conn.Open();

            NpgsqlCommand command;
            NpgsqlDataReader reader;
            string query;

            // Contando total de solicitudes
            query = "SELECT count(*) from jira_issue_report WHERE trim(description) <> '' AND reporter_id IS NOT NULL AND reporter_id > 0";
            command = new NpgsqlCommand(query, conn);
            decimal total_requests = Convert.ToDecimal(command.ExecuteScalar());

            // Contando solicitudes desatendidas
            query = "SELECT count(*) from jira_issue_report WHERE trim(description) <> '' AND assignee_id IS NULL AND LOWER(status) = 'open' AND reporter_id IS NOT NULL AND reporter_id > 0";
            command = new NpgsqlCommand(query, conn);
            decimal unattended_requests = Convert.ToDecimal(command.ExecuteScalar());

            // Obteniendo todos los repositorios
            var repositories = new List<string>();

            query = "SELECT DISTINCT(repositoryname) as rname FROM jira_issue_report";
            command = new NpgsqlCommand(query, conn);
            reader = command.ExecuteReader();
            while (reader.Read())
                repositories.Add(Convert.ToString(reader["rname"]));
            reader.Close();

            // Contando solicitudes desatendidas por repositorio
            var repositories_details = new Dictionary<string, decimal>();
            foreach (var repository in repositories)
            {
                query = "SELECT count(*) from jira_issue_report WHERE trim(description) <> '' AND assignee_id IS NULL AND LOWER(status) = 'open' AND reporter_id IS NOT NULL AND reporter_id > 0 AND repositoryname = @Repository";
                command = new NpgsqlCommand(query, conn);
                command.Parameters.Add(new NpgsqlParameter("@Repository", repository));

                repositories_details.Add(repository, Convert.ToDecimal(command.ExecuteScalar()));
            }

            // Imprimiendo reporte
            Console.WriteLine("Total de solicitudes: {0}", total_requests);
            Console.WriteLine("Total de solicitudes atendidas: {0} ({1:P2})", (total_requests - unattended_requests), ((total_requests - unattended_requests) / total_requests));
            Console.WriteLine("Total de solicitudes desatendidas: {0} ({1:P2})", unattended_requests, (unattended_requests / total_requests));
            foreach (var repository_detail in repositories_details)
                Console.WriteLine("Total de solicitudes desatendidas del repositorio \"{0}\": {1} ({2:P2})", repository_detail.Key, repository_detail.Value, (repository_detail.Value / unattended_requests));

            List<Requirement> requirements = new List<Requirement>();
            query = "SELECT id, description from jira_issue_report WHERE trim(description) <> '' AND assignee_id IS NULL AND LOWER(status) = 'open' AND reporter_id IS NOT NULL AND reporter_id > 0";
            command = new NpgsqlCommand(query, conn);
            reader = command.ExecuteReader();
            while (reader.Read())
                requirements.Add(new Requirement(Convert.ToInt32(reader["id"]), Convert.ToString(reader["description"])));
            reader.Close();

            searchReqSmells(requirements, false);
            commentsAnalysis(requirements);
        }
    }

    private static void reportSmells()
    {
        Console.WriteLine(Environment.NewLine);
        Console.WriteLine("Generando reporte de smells");

        ConnectionStringSettings settings = ConfigurationManager.ConnectionStrings["jira"];
        using (NpgsqlConnection conn = new NpgsqlConnection(settings.ConnectionString))
        {
            conn.Open();

            NpgsqlCommand command;
            NpgsqlDataReader reader;
            string query;

            List<Requirement> requirements = new List<Requirement>();
            query = "SELECT id, description from jira_issue_report WHERE trim(description) <> '' AND reporter_id IS NOT NULL AND reporter_id > 0";
            command = new NpgsqlCommand(query, conn);
            reader = command.ExecuteReader();
            while (reader.Read())
                requirements.Add(new Requirement(Convert.ToInt32(reader["id"]), Convert.ToString(reader["description"])));
            reader.Close();

            Console.WriteLine("Total de solicitudes: {0}", requirements.Count);
            searchReqSmells(requirements, true);
        }
    }

    private static void reportMismatch()
    {
        Console.WriteLine(Environment.NewLine);
        Console.WriteLine("Generando reporte de incongruencias");

        ConnectionStringSettings settings = ConfigurationManager.ConnectionStrings["jira"];
        using (NpgsqlConnection conn = new NpgsqlConnection(settings.ConnectionString))
        {
            conn.Open();

            NpgsqlCommand command;
            NpgsqlDataReader reader;
            string query;

            List<Requirement> requirements = new List<Requirement>();
            query = "SELECT count(*) from jira_issue_report WHERE trim(description) <> '' AND reporter_id IS NOT NULL AND reporter_id > 0";
            command = new NpgsqlCommand(query, conn);
            decimal total_requirements_count = Convert.ToDecimal(command.ExecuteScalar());
            Console.WriteLine("Total de solicitudes: {0}", total_requirements_count);

            // Buscando reqeurimientos con incongruencias
            List<Requirement> requirements_with_mismatch = new List<Requirement>();
            query = "SELECT A.id, A.description, COUNT(B.*) as fixes from jira_issue_report A LEFT JOIN jira_issue_fixversion B ON A.id=B.issue_id WHERE trim(description) <> '' AND reporter_id IS NOT NULL AND reporter_id > 0 GROUP BY A.id";
            command = new NpgsqlCommand(query, conn);
            reader = command.ExecuteReader();
            int fixes = 0;
            decimal requirements_without_mismatch_count = 0;
            while (reader.Read())
            {
                fixes = Convert.ToInt32(reader["fixes"]);
                if (fixes > 1)
                    requirements_with_mismatch.Add(new Requirement(Convert.ToInt32(reader["id"]), Convert.ToString(reader["description"])));
                else
                    requirements_without_mismatch_count++;
            }
            reader.Close();

            Console.WriteLine("Total de solicitudes sin incongruencias: {0} ({1:P2})", requirements_without_mismatch_count, (requirements_without_mismatch_count / total_requirements_count));
            Console.WriteLine("Total de solicitudes con incongruencias: {0} ({1:P2})", requirements_with_mismatch.Count, (requirements_with_mismatch.Count / total_requirements_count));

            searchReqSmells(requirements_with_mismatch, false);
            commentsAnalysis(requirements_with_mismatch);
        }
    }

    private static void searchReqSmells(List<Requirement> requirements, Boolean comment_analysis)
    {
        // Obtener listado de smells
        List<Smell> smells = new List<Smell>();
        ConnectionStringSettings settings = ConfigurationManager.ConnectionStrings["smells_catalog"];
        using (SQLiteConnection conn = new SQLiteConnection(settings.ConnectionString))
        {
            conn.Open();

            string query;
            SQLiteCommand command;
            SQLiteDataReader reader;

            // Creando tipos de smells
            query = "SELECT id, name FROM types";
            command = new SQLiteCommand(query, conn);
            reader = command.ExecuteReader();
            while (reader.Read())
                smells.Add(new Smell(Convert.ToInt32(reader["id"]), Convert.ToString(reader["name"])));
            reader.Close();

            // Creando frases de smells
            query = "SELECT value FROM smells WHERE type_id=@TypeId";
            command = new SQLiteCommand(query, conn);
            foreach (Smell smell in smells)
            {
                command.Parameters.Add(new SQLiteParameter("@TypeId", smell.Id));
                reader = command.ExecuteReader();
                while (reader.Read())
                    smell.Phrases.Add(Convert.ToString(reader["value"]));
                reader.Close();
            }
        }

        decimal total_smells_count = 0;
        foreach (Requirement requirement in requirements) 
        { 
            foreach (Smell smell in smells)
            {
                foreach (string phrase in smell.Phrases)
                {
                    if (requirement.Description.ToLower().Contains(" " + phrase.ToLower() + " "))
                    {
                        smell.Count++;
                        total_smells_count++;
                        requirement.WithSmell = true;
                    }
                }
            }
        }

        decimal count_requirements_with_smells = 0;
        decimal count_requirements_without_smells = 0;
        foreach (Requirement requirement in requirements)
        {
            if (requirement.WithSmell)
                count_requirements_with_smells++;
            else
                count_requirements_without_smells++;
        }

        Console.WriteLine("Total de solicitudes sin smells: {0} ({1:P2})", count_requirements_without_smells, (count_requirements_without_smells / requirements.Count));
        Console.WriteLine("Total de solicitudes con smells: {0} ({1:P2})", count_requirements_with_smells, (count_requirements_with_smells / requirements.Count));

        foreach (Smell smell in smells)
            Console.WriteLine("Total de solicitudes con smells de tipo \"{0}\": {1} ({2:P2})", smell.Name, smell.Count, (smell.Count / total_smells_count));

        if (comment_analysis)
        {
            List<Requirement> requirements_with_smells = new List<Requirement>();
            foreach (Requirement requirement in requirements)
            {
                if (requirement.WithSmell)
                    requirements_with_smells.Add(requirement);
            }

            commentsAnalysis(requirements_with_smells);
        }
    }

    private static void commentsAnalysis(List<Requirement> requirements)
    {
        ConnectionStringSettings settings = ConfigurationManager.ConnectionStrings["jira"];
        using (NpgsqlConnection conn = new NpgsqlConnection(settings.ConnectionString))
        {
            conn.Open();

            NpgsqlCommand command;
            NpgsqlDataReader reader;
            string query;

            foreach (Requirement requirement in requirements)
            {
                // Contando comentarios del issue
                query = "SELECT count(*) from jira_issue_comment WHERE issue_report_id=@RequirementId";
                command = new NpgsqlCommand(query, conn);
                command.Parameters.Add(new NpgsqlParameter("@RequirementId", requirement.Id));
                requirement.CommentsCount = Convert.ToDecimal(command.ExecuteScalar());

                // Buscando sentimientos del issue
                query = "SELECT COALESCE(SUM(anger_count), 0) as anger_count, COALESCE(SUM(joy_count),0) as joy_count, COALESCE(SUM(love_count), 0) as love_count, COALESCE(SUM(sadness_count), 0) as sadness_count from jira_issue_comment WHERE issue_report_id=@RequirementId";
                command = new NpgsqlCommand(query, conn);
                command.Parameters.Add(new NpgsqlParameter("@RequirementId", requirement.Id));
                reader = command.ExecuteReader();
                while (reader.Read())
                {
                    requirement.AngerCount = Convert.ToDecimal(reader["anger_count"]);
                    requirement.JoyCount = Convert.ToDecimal(reader["joy_count"]);
                    requirement.LoveCount = Convert.ToDecimal(reader["love_count"]);
                    requirement.SadnessCount = Convert.ToDecimal(reader["sadness_count"]);
                }
                reader.Close();
            }

            // Imprimiendo reporte
            decimal total_comments_count = 0;
            decimal total_anger_count = 0;
            decimal total_joy_count = 0;
            decimal total_love_count = 0;
            decimal total_sadness_count = 0;
            foreach (Requirement requirement in requirements)
            {
                total_comments_count += requirement.CommentsCount;
                total_anger_count += requirement.AngerCount;
                total_joy_count += requirement.JoyCount;
                total_love_count += requirement.LoveCount;
                total_sadness_count += requirement.SadnessCount;
            }

            Console.WriteLine("Total de comentarios: {0}", total_comments_count);
            Console.WriteLine("Promedio de comentarios por solicitud: {0:N2}", (total_comments_count / requirements.Count));
            
            Console.WriteLine("Total de sentimientos de ANGER en los comentarios: {0}", total_anger_count);
            Console.WriteLine("Total de sentimientos de JOY en los comentarios: {0}", total_joy_count);
            Console.WriteLine("Total de sentimientos de LOVE en los comentarios: {0}", total_love_count);
            Console.WriteLine("Total de sentimientos de SADNESS en los comentarios: {0}", total_sadness_count);

            Console.WriteLine("Promedio de sentimientos de ANGER en los comentarios: {0:N2}", (total_comments_count > 0) ? (total_anger_count / total_comments_count) : 0);
            Console.WriteLine("Total de sentimientos de JOY en los comentarios: {0:N2}", (total_comments_count > 0) ? (total_joy_count / total_comments_count) : 0);
            Console.WriteLine("Total de sentimientos de LOVE en los comentarios: {0:N2}", (total_comments_count > 0) ? (total_love_count / total_comments_count) : 0);
            Console.WriteLine("Total de sentimientos de SADNESS en los comentarios: {0:N2}", (total_comments_count > 0) ? (total_sadness_count / total_comments_count) : 0);
        }
    }
}
