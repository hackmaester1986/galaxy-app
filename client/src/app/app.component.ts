import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { GalaxyBrowserComponent } from "./components/galaxy-browser/galaxy-browser.component";

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, GalaxyBrowserComponent],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css'
})
export class AppComponent {
  title = 'client';
}
