import { Injectable } from '@angular/core';
import { HttpClient, HttpEvent } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ConfigService } from './config.service';

@Injectable({ providedIn: 'root' })
export class UploadService {
  constructor(private http: HttpClient, private config: ConfigService) {}

  upload(files: File[]): Observable<HttpEvent<any>> {
    const formData = new FormData();
    files.forEach(file => formData.append('files', file, file.webkitRelativePath || file.name));
    return this.http.post<any>(`${this.config.apiUrl}/upload`, formData, {
      reportProgress: true,
      observe: 'events'
    });
  }
}
