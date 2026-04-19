// Copyright (c) Mythetech. Licensed under the Elastic License 2.0.
import { useInvoke } from '@hermes/react';
import './SystemInfo.css';

function SystemInfo() {
  const runtime = useInvoke<string>('getRuntime');
  const platform = useInvoke<string>('getPlatform');

  return (
    <div className="card system-info">
      <h2>System Info</h2>
      <p>
        Runtime:{' '}
        <span className="info-value">
          {runtime.loading ? 'Loading...' : runtime.data ?? 'N/A'}
        </span>
      </p>
      <p>
        Platform:{' '}
        <span className="info-value">
          {platform.loading ? 'Loading...' : platform.data ?? 'N/A'}
        </span>
      </p>
    </div>
  );
}

export default SystemInfo;
