import { Component } from '@angular/core';
import { EmptyState } from '../../shared/components/empty-state/empty-state';

@Component({
  selector: 'app-profile-home',
  imports: [EmptyState],
  templateUrl: './profile-home.html',
  styleUrl: './profile-home.scss',
})
export class ProfileHome {}
