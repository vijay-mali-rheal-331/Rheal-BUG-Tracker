import { Injectable } from '@angular/core';
import { HttpClient, HttpEvent } from '@angular/common/http';
import { Observable, interval, switchMap, takeWhile, shareReplay } from 'rxjs';
import { ConfigService } from './config.service';
import { CreateSessionRequest, CreateSessionResponse, SessionDto } from '../models/session.model';
import { AnalysisReport } from '../models/report.model';

@Injectable({ providedIn: 'root' })
export class ApiService {
  constructor(private http: HttpClient, private config: ConfigService) {}

  private get base(): string {
    return this.config.apiUrl;
  }

  // ── Sessions ──────────────────────────────────────────────────────────────

  createSession(request: CreateSessionRequest): Observable<CreateSessionResponse> {
    return this.http.post<CreateSessionResponse>(`${this.base}/api/sessions`, request);
  }

  getSession(sessionId: string): Observable<SessionDto> {
    return this.http.get<SessionDto>(`${this.base}/api/sessions/${sessionId}`);
  }

  /** Polls session every 2 seconds until Completed or Failed. */
  pollSession(sessionId: string): Observable<SessionDto> {
    return interval(2000).pipe(
      switchMap(() => this.getSession(sessionId)),
      takeWhile(s => s.status === 'Pending' || s.status === 'Running', true)
    );
  }

  // ── Analysis ──────────────────────────────────────────────────────────────

  analyzeFiles(sessionId: string, files: File[]): Observable<HttpEvent<any>> {
    const formData = new FormData();
    files.forEach(file =>
      formData.append('files', file, file.webkitRelativePath || file.name)
    );
    return this.http.post<any>(
      `${this.base}/api/analyze/files?sessionId=${sessionId}`,
      formData,
      { reportProgress: true, observe: 'events' }
    );
  }

  analyzeGitHub(sessionId: string): Observable<any> {
    return this.http.post<any>(`${this.base}/api/analyze/github?sessionId=${sessionId}`, {});
  }

  // ── Reports ───────────────────────────────────────────────────────────────

  getReport(sessionId: string): Observable<AnalysisReport> {
    return this.http.get<AnalysisReport>(`${this.base}/api/reports/${sessionId}`);
  }

  getMarkdownReport(sessionId: string): Observable<string> {
    return this.http.get(`${this.base}/api/reports/${sessionId}/markdown`, { responseType: 'text' });
  }
}
