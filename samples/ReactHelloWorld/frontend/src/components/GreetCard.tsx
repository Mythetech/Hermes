import { useState } from 'react';
import { useInvoke } from '@hermes/bridge-react';
import './GreetCard.css';

function GreetCard() {
  const [name, setName] = useState('World');
  const { data, loading, error, invoke } = useInvoke<string>('greet');

  const handleGreet = async () => {
    try {
      await invoke(name);
    } catch {
      // Error captured in hook state
    }
  };

  return (
    <div className="card">
      <div className="greet-form">
        <input
          type="text"
          value={name}
          onChange={(e) => setName(e.target.value)}
          placeholder="Enter your name"
        />
        <button onClick={handleGreet} disabled={loading}>
          {loading ? 'Greeting...' : 'Greet from C#'}
        </button>
      </div>
      {data && <p className="output">{data}</p>}
      {error && <p className="output error">{error.message}</p>}
    </div>
  );
}

export default GreetCard;
