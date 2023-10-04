

using System.Data;
using System.Data.Common;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using Microsoft.Data.Sqlite;
using System.Data.SqlClient;
using System.ComponentModel.DataAnnotations;

static TcpConnectionInformation[] GetConnections() {
    IPGlobalProperties igp = IPGlobalProperties.GetIPGlobalProperties();
    TcpConnectionInformation[] connections = igp.GetActiveTcpConnections();
    return connections;
}



bool db_ready = false;

using (var connection = new SqliteConnection("Data Source=connections.db")) {


    bool try_db_setup = false;
    connection.Open();

    try {
        
        var cmd_checkdb = connection.CreateCommand();
        cmd_checkdb.CommandText = "SELECT SUM(1) AS RC FROM DBVERSION";
        var resp = cmd_checkdb.ExecuteScalar();
        if(resp == null) {
            db_ready = false;
            try_db_setup = true;
        } else if (resp.ToString() == "1") {
            db_ready = true;
        }
    } catch  {
        Console.WriteLine("[*] attempting db setup...");
        try_db_setup = true;
    }

    if (try_db_setup) {
        try {
            var cmd = connection.CreateCommand();
            cmd.CommandText =
                @"
            CREATE TABLE CONNECTIONS (
                id INTEGER PRIMARY KEY ASC,
                Recorded TEXT,
                Hostname TEXT ,
                LocalAddress TEXT,
                LocalPort INTEGER,
                RemoteAddress TEXT,
                RemotePort INTEGER,
                ConnectionState TEXT,
                FirstSeen INTEGER
            );            
            CREATE TABLE DBVERSION (
                id INTEGER PRIMARY KEY ASC,
                Version TEXT
            );
            INSERT INTO DBVERSION (Version) VALUES ('0.0.0');
        ";
            int resp = cmd.ExecuteNonQuery();
            if (resp == 1) {
                db_ready = true;
            }
        } catch {
            Console.WriteLine("[!] DB not ready");
        }       
    }
}

if (!db_ready) {
    Console.WriteLine("[!] could not prepare db, exiting");
    Environment.Exit(1);
}
Console.WriteLine("[*] Starting...");

TcpConnectionInformation[] connections = GetConnections();

if (connections == null) {
    Console.WriteLine("[!] could not get connection information");
    Environment.Exit(2);
}

string recorded = System.DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
string hostname = Environment.MachineName.ToUpper();
int run_lim = 5;
List<System.Net.IPAddress> newobs = new List<System.Net.IPAddress>();

//run checks 
using (var dbconn = new SqliteConnection("Data Source=connections.db")) {
    dbconn.Open();
    foreach (TcpConnectionInformation conn in connections) {
        var dbcmd = dbconn.CreateCommand();
        dbcmd.CommandText = @"SELECT COUNT(1) RC FROM CONNECTIONS WHERE RemoteAddress = $ra";
        
        SqliteParameter ra = dbcmd.CreateParameter();
        ra.ParameterName = "$ra";
        ra.Value = conn.RemoteEndPoint.Address.ToString();

        dbcmd.Parameters.Add(ra);
        SqliteDataReader reader = dbcmd.ExecuteReader();
        reader.Read();       
        
        if(reader[0].ToString() == "0") { //never seen
            Console.WriteLine($"[*] We never saw {conn.RemoteEndPoint.Address}");
            newobs.Add(conn.RemoteEndPoint.Address);
        }
    }
}



using (var dbconn = new SqliteConnection("Data Source=connections.db")) {
    dbconn.Open();
    using (var tx = dbconn.BeginTransaction()) {
        var dbcmd = dbconn.CreateCommand();
        dbcmd.CommandText = @"INSERT INTO CONNECTIONS (Recorded,hostname,LocalAddress,LocalPort,RemoteAddress,RemotePort,ConnectionState, FirstSeen) VALUES ($rec, $hn, $la, $lp, $ra,$rp,$ns,$fs)";


        SqliteParameter param_rec = dbcmd.CreateParameter();
        param_rec.ParameterName = "$rec";

        SqliteParameter param_hn = dbcmd.CreateParameter();
        param_hn.ParameterName = "$hn";

        SqliteParameter param_la = dbcmd.CreateParameter();
        param_la.ParameterName = "$la";

        SqliteParameter param_lp = dbcmd.CreateParameter();
        param_lp.ParameterName = "$lp";

        SqliteParameter param_ra = dbcmd.CreateParameter();
        param_ra.ParameterName = "$ra";

        SqliteParameter param_rp = dbcmd.CreateParameter();
        param_rp.ParameterName = "$rp";

        SqliteParameter param_ns = dbcmd.CreateParameter();
        param_ns.ParameterName = "$ns";

        SqliteParameter param_fs = dbcmd.CreateParameter();
        param_fs.ParameterName = "$fs";

        dbcmd.Parameters.Add(param_rec);
        dbcmd.Parameters.Add(param_hn);
        dbcmd.Parameters.Add(param_la);
        dbcmd.Parameters.Add(param_lp);
        dbcmd.Parameters.Add(param_ra);
        dbcmd.Parameters.Add(param_rp);
        dbcmd.Parameters.Add(param_ns);
        dbcmd.Parameters.Add(param_fs);

        foreach (TcpConnectionInformation conn in connections) {
            string localaddress = conn.LocalEndPoint.Address.ToString();
            int localport = conn.LocalEndPoint.Port;
            string remoteaddress = conn.RemoteEndPoint.Address.ToString();
            int remoteport = conn.RemoteEndPoint.Port;
            string netconn_state = conn.State.ToString();

            param_rec.Value = recorded;
            param_hn.Value = hostname;

            param_la.Value = localaddress;
            param_lp.Value = localport;
            param_ra.Value = remoteaddress;
            param_rp.Value = remoteport;
            param_ns.Value = netconn_state;
            var new_remote = newobs.Any(x => x == conn.RemoteEndPoint.Address);
            if(new_remote ) {
                param_fs.Value = 1;
            }else {
                param_fs.Value = 0;
            }
            dbcmd.ExecuteNonQuery();
        }

        tx.Commit();
    }
}

//prune db 

using (var dbconn = new SqliteConnection("Data Source=connections.db")) {
    dbconn.Open();

    SqliteCommand runcount = dbconn.CreateCommand();

    runcount.CommandText = "SELECT COUNT(DISTINCT Recorded) AS DC FROM CONNECTIONS";
    SqliteDataReader reader = runcount.ExecuteReader();
    reader.Read();
    if (int.Parse(reader[0].ToString()) > run_lim) {
        SqliteCommand get_recs = dbconn.CreateCommand();
        get_recs.CommandText = "SELECT DISTINCT Recorded FROM CONNECTIONS ORDER BY RECORDED";
        SqliteDataReader results = get_recs.ExecuteReader();
        results.Read();
        string rtd = results[0].ToString();
        // todo: handle when number of runs is greater than limit by 2 or more 
        SqliteCommand del_old = dbconn.CreateCommand();
        del_old.CommandText = "DELETE FROM CONNECTIONS WHERE Recorded = $r";

        SqliteParameter rdel = del_old.CreateParameter();
        rdel.ParameterName = "$r";
        rdel.Value = rtd;
        del_old.Parameters.Add(rdel);
        del_old.ExecuteNonQuery();
    }
}



Console.WriteLine("[.] Done.");

