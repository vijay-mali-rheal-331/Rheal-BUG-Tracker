import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { of } from 'rxjs';

export interface AppConfig {
  apiUrl: string;
}

const DEFAULT_CONFIG: AppConfig = {
  apiUrl: 'http://localhost:5000'
};

@Injectable({ providedIn: 'root' })
export class ConfigService {
  private config: AppConfig = { ...DEFAULT_CONFIG };

  constructor(private http: HttpClient) {}

  load(): Promise<void> {
    return firstValueFrom(
      this.http.get<AppConfig>('/config.json').pipe(
        catchError(() => of(DEFAULT_CONFIG))
      )
    ).then(data => { this.config = data; });
  }

  get apiUrl(): string {
    return this.config.apiUrl;
  }
}
