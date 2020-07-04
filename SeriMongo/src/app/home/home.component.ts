import { Component, OnInit } from '@angular/core';

import { HubConnectionBuilder } from '@microsoft/signalr';
import { LogLevel } from '@microsoft/signalr';

import { LogEntry } from '../common/log-entry';

@Component({
  selector: 'app-home',
  templateUrl: './home.component.html',
  styleUrls: ['./home.component.css']
})
export class HomeComponent implements OnInit {

  logEntries: LogEntry[] = [];

  selectedRow: LogEntry;

  constructor() {
    // this.startSignalR();
  }

  startSignalR = async () => {
    const connection = new HubConnectionBuilder()
      .configureLogging(LogLevel.Debug)
      .withUrl('http://localhost:51983/logs', {
        // accessTokenFactory: () => this.loginToken

      })
      // .withAutomaticReconnect([0, 3000, 5000, 10000, 15000, 30000])
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: retryContext => {
            if (retryContext.elapsedMilliseconds < 60000) {
                // If we've been reconnecting for less than 60 seconds so far,
                // wait between 0 and 10 seconds before the next reconnect attempt.
                return Math.random() * 10000;
            } else {
                // If we've been reconnecting for more than 60 seconds so far, stop reconnecting.
                return null;
            }
        }
    })
    .build();

    try {
      await connection.start();
    } catch (error) {
      this.handleError(error)
    }

    /*
    connection.onreconnecting(error => {

    });

    connection.onreconnected(connectionId => {

    });
    */

    // connection.on('OnReceiveLogEntry', this.receiveLogEntry);
  }

  ngOnInit() {
    this.getLogEntries();
  }

  receiveLogEntry = (logEntry: LogEntry) => {
    // console.log(logEntry);

    // Add to begining
    this.logEntries.unshift(logEntry);

    // Add to end
    //const l = new LogEntry();
    //l.renderedMessage = logEntry.renderedMessage;
    //this.logEntries.push(l);
    console.log(JSON.stringify(this.logEntries));
  }

  handleError(error: any) {
    console.error(error);
  }

  setClickedRow(log: LogEntry) {
    this.selectedRow = log;
    log.showDetails = !log.showDetails;
  }

  getLogEntries = () => {
    this.logEntries = [
      {
        "id": "5efb2210382e47ef306524b7",

        "timestamp": new Date("2020-06-30T08:29:20.116-03:00"),
        "level": "Information",
        "renderedMessage": "This is a random \"Information\" log entry for \"\"Jaeden Gibson\"\" (\"Jaeden.Gibson84@gmail.com\") from \"\"Tunisia\"\"",
        "properties": {
          "CustomerName": "Jaeden Gibson",
          "Email": "Jaeden.Gibson84@gmail.com",
          "Country": "Tunisia"
        }
      },
      {
        "id": "5efb2210382e47ef306524b6",
        "timestamp": "2020-06-30T08:29:19.883-03:00",
        "level": "Warning",
        "renderedMessage": "This is a random \"Warning\" log entry for \"\"Retta Labadie\"\" (\"Retta.Labadie88@hotmail.com\") from \"\"Hong Kong\"\"",
        "properties": {
          "CustomerName": "Retta Labadie",
          "Email": "Retta.Labadie88@hotmail.com",
          "Country": "Hong Kong"
        }
      },
      {
        "id": "5efb2210382e47ef306524b5",
        "timestamp": "2020-06-30T08:29:19.506-03:00",
        "level": "Warning",
        "renderedMessage": "This is a random \"Warning\" log entry for \"\"Jaeden Block\"\" (\"Jaeden.Block@yahoo.com\") from \"\"Thailand\"\"",
        "properties": {
          "CustomerName": "Jaeden Block",
          "Email": "Jaeden.Block@yahoo.com",
          "Country": "Thailand"
        }
      },
      {
        "id": "5efb2210382e47ef306524b4",
        "timestamp": "2020-06-30T08:29:18.781-03:00",
        "level": "Error",
        "renderedMessage": "This is a random \"Error\" log entry for \"\"Allen Cormier\"\" (\"Allen44@gmail.com\") from \"\"Montserrat\"\"",
        "properties": {
          "CustomerName": "Allen Cormier",
          "Email": "Allen44@gmail.com",
          "Country": "Montserrat"
        }
      },
      {
        "id": "5efb220e382e47ef306524b3",
        "timestamp": "2020-06-30T08:29:17.424-03:00",
        "level": "Information",
        "renderedMessage": "This is a random \"Information\" log entry for \"\"Porter Aufderhar\"\" (\"Porter_Aufderhar@gmail.com\") from \"\"Bangladesh\"\"",
        "properties": {
          "CustomerName": "Porter Aufderhar",
          "Email": "Porter_Aufderhar@gmail.com",
          "Country": "Bangladesh"
        }
      },
      {
        "id": "5efb220e382e47ef306524b2",
        "timestamp": "2020-06-30T08:29:16.947-03:00",
        "level": "Warning",
        "renderedMessage": "This is a random \"Warning\" log entry for \"\"Alvis Klocko\"\" (\"Alvis_Klocko@hotmail.com\") from \"\"Pakistan\"\"",
        "properties": {
          "CustomerName": "Alvis Klocko",
          "Email": "Alvis_Klocko@hotmail.com",
          "Country": "Pakistan"
        }
      },
      {
        "id": "5efb220c382e47ef306524b1",
        "timestamp": "2020-06-30T08:29:16.048-03:00",
        "level": "Information",
        "renderedMessage": "This is a random \"Information\" log entry for \"\"Brook Conroy\"\" (\"Brook_Conroy@gmail.com\") from \"\"Haiti\"\"",
        "properties": {
          "CustomerName": "Brook Conroy",
          "Email": "Brook_Conroy@gmail.com",
          "Country": "Haiti"
        }
      },
      {
        "id": "5efb220c382e47ef306524b0",
        "timestamp": "2020-06-30T08:29:15.783-03:00",
        "level": "Error",
        "renderedMessage": "This is a random \"Error\" log entry for \"\"Trey Fritsch\"\" (\"Trey.Fritsch83@gmail.com\") from \"\"Ethiopia\"\"",
        "properties": {
          "CustomerName": "Trey Fritsch",
          "Email": "Trey.Fritsch83@gmail.com",
          "Country": "Ethiopia"
        }
      },
      {
        "id": "5efb220a382e47ef306524af",
        "timestamp": "2020-06-30T08:29:14.076-03:00",
        "level": "Information",
        "renderedMessage": "This is a random \"Information\" log entry for \"\"Helga Medhurst\"\" (\"Helga_Medhurst29@yahoo.com\") from \"\"Canada\"\"",
        "properties": {
          "CustomerName": "Helga Medhurst",
          "Email": "Helga_Medhurst29@yahoo.com",
          "Country": "Canada"
        }
      },
      {
        "id": "5efb220a382e47ef306524ae",
        "timestamp": "2020-06-30T08:29:12.992-03:00",
        "level": "Information",
        "renderedMessage": "This is a random \"Information\" log entry for \"\"Kay Mayer\"\" (\"Kay.Mayer5@hotmail.com\") from \"\"Burundi\"\"",
        "properties": {
          "CustomerName": "Kay Mayer",
          "Email": "Kay.Mayer5@hotmail.com",
          "Country": "Burundi"
        }
      },
      {
        "id": "5efb2208382e47ef306524ad",
        "timestamp": "2020-06-30T08:29:12.302-03:00",
        "level": "Warning",
        "renderedMessage": "This is a random \"Warning\" log entry for \"\"Lyda Bartoletti\"\" (\"Lyda_Bartoletti86@yahoo.com\") from \"\"Micronesia\"\"",
        "properties": {
          "CustomerName": "Lyda Bartoletti",
          "Email": "Lyda_Bartoletti86@yahoo.com",
          "Country": "Micronesia"
        }
      },
      {
        "id": "5efb2208382e47ef306524ac",
        "timestamp": "2020-06-30T08:29:11.395-03:00",
        "level": "Information",
        "renderedMessage": "This is a random \"Information\" log entry for \"\"Eden Hilll\"\" (\"Eden.Hilll@gmail.com\") from \"\"Japan\"\"",
        "properties": {
          "CustomerName": "Eden Hilll",
          "Email": "Eden.Hilll@gmail.com",
          "Country": "Japan"
        }
      },
      {
        "id": "5efb2206382e47ef306524ab",
        "timestamp": "2020-06-30T08:29:08.642-03:00",
        "level": "Information",
        "renderedMessage": "This is a random \"Information\" log entry for \"\"Donna Blick\"\" (\"Donna.Blick@hotmail.com\") from \"\"Benin\"\"",
        "properties": {
          "CustomerName": "Donna Blick",
          "Email": "Donna.Blick@hotmail.com",
          "Country": "Benin"
        }
      },
      {
        "id": "5efb2204382e47ef306524aa",
        "timestamp": "2020-06-30T08:29:08.334-03:00",
        "level": "Information",
        "renderedMessage": "This is a random \"Information\" log entry for \"\"Amara Pagac\"\" (\"Amara31@yahoo.com\") from \"\"Gibraltar\"\"",
        "properties": {
          "CustomerName": "Amara Pagac",
          "Email": "Amara31@yahoo.com",
          "Country": "Gibraltar"
        }
      },
      {
        "id": "5efb2204382e47ef306524a9",
        "timestamp": "2020-06-30T08:29:08.128-03:00",
        "level": "Warning",
        "renderedMessage": "This is a random \"Warning\" log entry for \"\"Melba Boyer\"\" (\"Melba.Boyer27@yahoo.com\") from \"\"Portugal\"\"",
        "properties": {
          "CustomerName": "Melba Boyer",
          "Email": "Melba.Boyer27@yahoo.com",
          "Country": "Portugal"
        }
      },
      {
        "id": "5efb2204382e47ef306524a8",
        "timestamp": "2020-06-30T08:29:07.635-03:00",
        "level": "Error",
        "renderedMessage": "This is a random \"Error\" log entry for \"\"Gonzalo Douglas\"\" (\"Gonzalo_Douglas@gmail.com\") from \"\"San Marino\"\"",
        "properties": {
          "CustomerName": "Gonzalo Douglas",
          "Email": "Gonzalo_Douglas@gmail.com",
          "Country": "San Marino"
        }
      },
      {
        "id": "5efb2204382e47ef306524a7",
        "timestamp": "2020-06-30T08:29:06.757-03:00",
        "level": "Warning",
        "renderedMessage": "This is a random \"Warning\" log entry for \"\"Murphy Lynch\"\" (\"Murphy_Lynch@hotmail.com\") from \"\"Zambia\"\"",
        "properties": {
          "CustomerName": "Murphy Lynch",
          "Email": "Murphy_Lynch@hotmail.com",
          "Country": "Zambia"
        }
      },
      {
        "id": "5efb2202382e47ef306524a6",
        "timestamp": "2020-06-30T08:29:06.147-03:00",
        "level": "Error",
        "renderedMessage": "This is a random \"Error\" log entry for \"\"Tatyana Harvey\"\" (\"Tatyana14@yahoo.com\") from \"\"Greece\"\"",
        "properties": {
          "CustomerName": "Tatyana Harvey",
          "Email": "Tatyana14@yahoo.com",
          "Country": "Greece"
        }
      },
      {
        "id": "5efb2202382e47ef306524a5",
        "timestamp": "2020-06-30T08:29:05.239-03:00",
        "level": "Warning",
        "renderedMessage": "This is a random \"Warning\" log entry for \"\"Lydia King\"\" (\"Lydia89@hotmail.com\") from \"\"Syrian Arab Republic\"\"",
        "properties": {
          "CustomerName": "Lydia King",
          "Email": "Lydia89@hotmail.com",
          "Country": "Syrian Arab Republic"
        }
      },
      {
        "id": "5efb2202382e47ef306524a4",
        "timestamp": "2020-06-30T08:29:04.708-03:00",
        "level": "Information",
        "renderedMessage": "This is a random \"Information\" log entry for \"\"Eusebio Durgan\"\" (\"Eusebio.Durgan@gmail.com\") from \"\"Malta\"\"",
        "properties": {
          "CustomerName": "Eusebio Durgan",
          "Email": "Eusebio.Durgan@gmail.com",
          "Country": "Malta"
        }
      },
      {
        "id": "5efb2200382e47ef306524a3",
        "timestamp": "2020-06-30T08:29:03.111-03:00",
        "level": "Warning",
        "renderedMessage": "This is a random \"Warning\" log entry for \"\"Jeanne Graham\"\" (\"Jeanne45@yahoo.com\") from \"\"Switzerland\"\"",
        "properties": {
          "CustomerName": "Jeanne Graham",
          "Email": "Jeanne45@yahoo.com",
          "Country": "Switzerland"
        }
      },
      {
        "id": "5efb2200382e47ef306524a2",
        "timestamp": "2020-06-30T08:29:02.44-03:00",
        "level": "Information",
        "renderedMessage": "This is a random \"Information\" log entry for \"\"Wayne Larkin\"\" (\"Wayne73@hotmail.com\") from \"\"Qatar\"\"",
        "properties": {
          "CustomerName": "Wayne Larkin",
          "Email": "Wayne73@hotmail.com",
          "Country": "Qatar"
        }
      },
      {
        "id": "5efb21fe382e47ef306524a1",
        "timestamp": "2020-06-30T08:29:01.876-03:00",
        "level": "Error",
        "renderedMessage": "This is a random \"Error\" log entry for \"\"Chelsie Klocko\"\" (\"Chelsie17@hotmail.com\") from \"\"Wallis and Futuna\"\"",
        "properties": {
          "CustomerName": "Chelsie Klocko",
          "Email": "Chelsie17@hotmail.com",
          "Country": "Wallis and Futuna"
        }
      },
      {
        "id": "5efb21fe382e47ef306524a0",
        "timestamp": "2020-06-30T08:29:01.112-03:00",
        "level": "Information",
        "renderedMessage": "This is a random \"Information\" log entry for \"\"Tabitha Zemlak\"\" (\"Tabitha.Zemlak17@gmail.com\") from \"\"Iceland\"\"",
        "properties": {
          "CustomerName": "Tabitha Zemlak",
          "Email": "Tabitha.Zemlak17@gmail.com",
          "Country": "Iceland"
        }
      }
    ] as unknown as LogEntry[];
  }


}
