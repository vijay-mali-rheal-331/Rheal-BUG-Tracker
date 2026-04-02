export type IssueSeverity = 'Critical' | 'High' | 'Medium' | 'Low';
export type IssueType = 'Bug' | 'Validation' | 'Security' | 'Performance' | 'CodeSmell' | 'EdgeCase';

export interface FileIssue {
  type: IssueType;
  severity: IssueSeverity;
  line: number;
  description: string;
  suggestion: string;
}

export interface FileResult {
  file: string;
  issueCount: number;
  issues: FileIssue[];
}

export interface ReportSummary {
  totalIssues: number;
  critical: number;
  high: number;
  medium: number;
  low: number;
}

export interface AnalysisReport {
  sessionId: string;
  status: string;
  createdAt: string;
  totalFiles: number;
  processedFiles: number;
  summary: ReportSummary;
  files: FileResult[];
}
