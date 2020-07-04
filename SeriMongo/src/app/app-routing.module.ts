import { NgModule } from '@angular/core';
import { Routes, RouterModule } from '@angular/router';

import { HomeComponent } from './home/home.component';
import { LogsComponent } from './logs/logs.component';

const routes: Routes = [
    { path: '', redirectTo: 'logs', pathMatch: 'full' },
    { path: 'home', component: HomeComponent },
    { path: 'logs', component: LogsComponent },

    // otherwise redirect to home
    { path: '**', redirectTo: 'home' }
];


@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule { }
