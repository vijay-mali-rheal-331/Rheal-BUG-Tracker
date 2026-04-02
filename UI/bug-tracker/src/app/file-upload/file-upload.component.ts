import { Component, OnDestroy } from '@angular/core';
import { HttpEventType, type HttpEvent } from '@angular/common/http';
import { Subscription } from 'rxjs';
import { ApiService } from '../services/api.service';
import { SessionDto } from '../models/session.model';
import { AnalysisReport, FileResult, IssueSeverity } from '../models/report.model';

export type WizardStep = 'setup' | 'source' | 'analyzing' | 'results';
export type SourceType = 'files' | 'github';

@Component({
  selector: 'app-file-upload',
  standalone: false,
  templateUrl: './file-upload.component.html',
  styleUrl: './file-upload.component.scss'
})
export class FileUploadComponent implements OnDestroy {

  // ── Wizard ───────────────────────────────────────────────────────────────
  step: WizardStep = 'setup';
  sourceType: SourceType = 'files';

  // ── Step 1: Setup ────────────────────────────────────────────────────────
  repoUrl = '';
  branch = '';

  // ── Step 2: Files ────────────────────────────────────────────────────────
  selectedFiles: File[] = [];
  uploadProgress = 0;

  // ── Step 3: Analysing ────────────────────────────────────────────────────
  sessionId = '';
  session: SessionDto | null = null;
  analyzeError = '';

  // ── Step 4: Results ──────────────────────────────────────────────────────
  report: AnalysisReport | null = null;
  severityFilter: IssueSeverity | 'All' = 'All';
  expandedFiles = new Set<string>();

  private subs = new Subscription();

  constructor(private api: ApiService) {}

  ngOnDestroy(): void { this.subs.unsubscribe(); }

  // ── Navigation ───────────────────────────────────────────────────────────

  goToSource(): void {
    this.step = 'source';
  }

  backToSetup(): void {
    this.step = 'setup';
    this.analyzeError = '';
  }

  startAnalysis(): void {
    if (this.sourceType === 'files' && !this.selectedFiles.length) return;
    if (this.sourceType === 'github' && !this.repoUrl.trim()) return;
    this.analyzeError = '';
    this.step = 'analyzing';
    this.session = null;
    this.uploadProgress = 0;

    this.subs.add(
      this.api.createSession({
        repoUrl: this.repoUrl.trim() || undefined,
        branch: this.branch.trim() || undefined
      }).subscribe({
        next: (res: import('../models/session.model').CreateSessionResponse) => {
          this.sessionId = res.sessionId;
          this.sourceType === 'files' ? this.doFileAnalysis() : this.doGitHubAnalysis();
        },
        error: (err: unknown) => this.handleError(err)
      })
    );
  }

  // ── Analysis ─────────────────────────────────────────────────────────────

  private doFileAnalysis(): void {
    this.subs.add(
      this.api.analyzeFiles(this.sessionId, this.selectedFiles).subscribe({
        next: (event: HttpEvent<unknown>) => {
          if (event.type === HttpEventType.UploadProgress && event.total)
            this.uploadProgress = Math.round((100 * event.loaded) / event.total);
          else if (event.type === HttpEventType.Response)
            this.startPolling();
        },
        error: (err: unknown) => this.handleError(err)
      })
    );
  }

  private doGitHubAnalysis(): void {
    this.subs.add(
      this.api.analyzeGitHub(this.sessionId).subscribe({
        next: () => this.startPolling(),
        error: (err: unknown) => this.handleError(err)
      })
    );
  }

  private startPolling(): void {
    this.subs.add(
      this.api.pollSession(this.sessionId).subscribe({
        next: (s: SessionDto) => {
          this.session = s;
          if (s.status === 'Completed') this.loadReport();
          if (s.status === 'Failed') this.handleError(new Error(s.errorMessage ?? 'Analysis failed'));
        },
        error: (err: unknown) => this.handleError(err)
      })
    );
  }

  private loadReport(): void {
    this.subs.add(
      this.api.getReport(this.sessionId).subscribe({
        next: (report: AnalysisReport) => { this.report = report; this.step = 'results'; },
        error: (err: unknown) => this.handleError(err)
      })
    );
  }

  private handleError(err: unknown): void {
    const e = err as { error?: { error?: string }; message?: string };
    this.analyzeError = e?.error?.error ?? e?.message ?? 'An unexpected error occurred.';
  }

  // ── File management ──────────────────────────────────────────────────────

  onFilesSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (!input.files) return;
    Array.from(input.files).forEach(f => {
      if (!this.selectedFiles.find(x => x.name === f.name && x.size === f.size))
        this.selectedFiles.push(f);
    });
    input.value = '';
  }

  removeFile(index: number): void { this.selectedFiles.splice(index, 1); }
  clearFiles(): void { this.selectedFiles = []; }

  // ── Results helpers ──────────────────────────────────────────────────────

  get filteredFiles(): FileResult[] {
    if (!this.report) return [];
    if (this.severityFilter === 'All') return this.report.files.filter(f => f.issues.length);
    return this.report.files
      .map(f => ({ ...f, issues: f.issues.filter(i => i.severity === this.severityFilter) }))
      .filter(f => f.issues.length > 0);
  }

  toggleFile(file: string): void {
    this.expandedFiles.has(file) ? this.expandedFiles.delete(file) : this.expandedFiles.add(file);
  }

  isExpanded(file: string): boolean { return this.expandedFiles.has(file); }

  downloadMarkdown(): void {
    this.subs.add(
      this.api.getMarkdownReport(this.sessionId).subscribe(md => {
        const blob = new Blob([md], { type: 'text/markdown' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `report-${this.sessionId.slice(0, 8)}.md`;
        a.click();
        URL.revokeObjectURL(url);
      })
    );
  }

  startOver(): void {
    this.subs.unsubscribe();
    this.subs = new Subscription();
    this.step = 'setup';
    this.repoUrl = '';
    this.branch = '';
    this.selectedFiles = [];
    this.uploadProgress = 0;
    this.sessionId = '';
    this.session = null;
    this.report = null;
    this.analyzeError = '';
    this.expandedFiles.clear();
    this.severityFilter = 'All';
  }

  // ── Utils ────────────────────────────────────────────────────────────────

  formatSize(bytes: number): string {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1048576) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / 1048576).toFixed(1)} MB`;
  }

  issueTypeIcon(type: string): string {
    const map: Record<string, string> = {
      Bug: '🐛', Validation: '✅', Security: '🔒',
      Performance: '⚡', CodeSmell: '🤢', EdgeCase: '🔍'
    };
    return map[type] ?? '•';
  }

  // ── Step indicator helpers ────────────────────────────────────────────────

  isStepActive(n: number): boolean {
    const order: WizardStep[] = ['setup', 'source', 'analyzing', 'results'];
    return order[n - 1] === this.step;
  }

  isStepDone(n: number): boolean {
    const order: WizardStep[] = ['setup', 'source', 'analyzing', 'results'];
    return order.indexOf(this.step) > n - 1;
  }
}
